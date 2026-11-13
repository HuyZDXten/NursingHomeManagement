import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { jwtDecode } from 'jwt-decode';
import { authApi } from '../api/auth';
import {
  clearSession, getAccessToken, getRefreshToken, isExpiringSoon, onSessionChange, refreshSession, storeSession,
} from '../api/client';
import type { LoginResponse, Role } from '../types';

export interface AuthUser {
  accountId: number;
  name: string;
  email: string;
  role: Role;
}

// API tắt MapInboundClaims nên JWT mang tên claim ngắn.
interface DecodedToken {
  sub: string;
  name: string;
  email: string;
  role: Role;
  exp: number;
}

function decodeUser(token: string | null): AuthUser | null {
  if (!token) return null;
  try {
    const decoded = jwtDecode<DecodedToken>(token);
    if (decoded.exp * 1000 < Date.now() && !getRefreshToken()) return null;
    return { accountId: Number(decoded.sub), name: decoded.name, email: decoded.email, role: decoded.role };
  } catch {
    return null;
  }
}

interface AuthContextValue {
  user: AuthUser | null;
  login: (email: string, password: string) => Promise<AuthUser>;
  /** Dùng phiên mới do API cấp ngoài đăng nhập (sau khi đổi mật khẩu). */
  applySession: (session: LoginResponse) => void;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => decodeUser(getAccessToken()));

  useEffect(() => onSessionChange((token) => setUser(decodeUser(token))), []);

  useEffect(() => {
    const token = getAccessToken();
    if (token && isExpiringSoon(token) && getRefreshToken()) void refreshSession();
  }, []);

  const value = useMemo<AuthContextValue>(() => {
    const start = (session: LoginResponse): AuthUser => {
      const decoded = decodeUser(session.accessToken);
      if (!decoded) throw new Error('Máy chủ trả về token không hợp lệ.');
      storeSession(session);
      return decoded;
    };
    return {
      user,
      login: async (email, password) => start(await authApi.login(email, password)),
      applySession: (session) => {
        start(session);
      },
      logout: async () => {
        const refreshToken = getRefreshToken();
        clearSession();
        if (refreshToken) {
          try {
            await authApi.logout(refreshToken);
          } catch {
            // Đã xóa phiên ở máy; lỗi mạng khi thu hồi token không cần báo.
          }
        }
      },
    };
  }, [user]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth phải nằm trong <AuthProvider>.');
  return context;
}
