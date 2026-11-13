import axios, { type InternalAxiosRequestConfig } from 'axios';
import { jwtDecode } from 'jwt-decode';
import type { ApiError, LoginResponse } from '../types';

// Cùng cơ chế với ASS2: access token ngắn hạn + refresh token xoay vòng, tự làm mới trước khi hết hạn,
// một 401 thì làm mới một lần rồi gửi lại request.
const ACCESS_TOKEN_KEY = 'nursinghome.accessToken';
const REFRESH_TOKEN_KEY = 'nursinghome.refreshToken';
const EXPIRY_MARGIN_MS = 30_000;

export const API_BASE_URL: string = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';

export const apiClient = axios.create({ baseURL: API_BASE_URL });

// ---------- Lưu phiên ----------

type SessionListener = (accessToken: string | null) => void;
const listeners = new Set<SessionListener>();

export function onSessionChange(listener: SessionListener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

function notify() {
  const token = getAccessToken();
  listeners.forEach((listener) => listener(token));
}

window.addEventListener('storage', (e) => {
  if (e.key === null || e.key === ACCESS_TOKEN_KEY) notify();
});

export const getAccessToken = () => localStorage.getItem(ACCESS_TOKEN_KEY);
export const getRefreshToken = () => localStorage.getItem(REFRESH_TOKEN_KEY);

export function storeSession(session: Pick<LoginResponse, 'accessToken' | 'refreshToken'>): void {
  localStorage.setItem(ACCESS_TOKEN_KEY, session.accessToken);
  localStorage.setItem(REFRESH_TOKEN_KEY, session.refreshToken);
  notify();
}

export function clearSession(): void {
  localStorage.removeItem(ACCESS_TOKEN_KEY);
  localStorage.removeItem(REFRESH_TOKEN_KEY);
  notify();
}

export function isExpiringSoon(token: string): boolean {
  try {
    return jwtDecode<{ exp: number }>(token).exp * 1000 - EXPIRY_MARGIN_MS < Date.now();
  } catch {
    return true;
  }
}

// ---------- Làm mới phiên ----------

let refreshing: Promise<string | null> | null = null;

/** Các request song song dùng chung MỘT lần refresh (API thu hồi refresh token ngay lần dùng đầu). */
export function refreshSession(): Promise<string | null> {
  refreshing ??= (async () => {
    const refreshToken = getRefreshToken();
    if (!refreshToken) return null;
    try {
      const { data } = await axios.post<LoginResponse>(`${API_BASE_URL}/api/auth/refresh`, { refreshToken });
      storeSession(data);
      return data.accessToken;
    } catch (error) {
      if (axios.isAxiosError(error) && error.response?.status === 401) clearSession();
      return null;
    }
  })().finally(() => {
    refreshing = null;
  });
  return refreshing;
}

/** Token hợp lệ cho SignalR (tự làm mới nếu sắp hết hạn). */
export async function getFreshAccessToken(): Promise<string> {
  const token = getAccessToken();
  if (token && isExpiringSoon(token) && getRefreshToken()) return (await refreshSession()) ?? token;
  return token ?? '';
}

// ---------- Interceptors ----------

apiClient.interceptors.request.use(async (config) => {
  let token = getAccessToken();
  if (token && isExpiringSoon(token) && getRefreshToken()) {
    token = (await refreshSession()) ?? token;
  }
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

interface RetriableRequest extends InternalAxiosRequestConfig {
  _retried?: boolean;
}

apiClient.interceptors.response.use(undefined, async (error) => {
  if (!axios.isAxiosError(error) || error.response?.status !== 401 || !error.config) {
    return Promise.reject(error);
  }
  const config = error.config as RetriableRequest;
  const sentToken = String(config.headers?.Authorization ?? '').replace(/^Bearer /, '');
  const tokenExpired = error.response.headers['token-expired'] === 'true';
  const isAuthEndpoint = String(config.url ?? '').startsWith('/api/auth/');
  if (!sentToken || (isAuthEndpoint && !tokenExpired)) return Promise.reject(error);

  if (!config._retried) {
    config._retried = true;
    const current = getAccessToken();
    const token = current && current !== sentToken ? current : await refreshSession();
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
      return apiClient(config);
    }
  }

  clearSession();
  if (window.location.pathname !== '/login') window.location.assign('/login?expired=1');
  return Promise.reject(error);
});

// ---------- Lỗi ----------

interface ProblemDetails {
  title?: string;
  errors?: Record<string, string[]>;
}

/** Lấy thông báo lỗi từ { message } của API hoặc ProblemDetails khi validation [ApiController] thất bại. */
export function extractErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as (ApiError & ProblemDetails) | undefined;
    if (data?.message) return data.message;
    if (data?.errors) {
      const first = Object.values(data.errors)[0]?.[0];
      if (first) return first;
    }
    if (data?.title) return data.title;
    if (!error.response) return 'Không kết nối được máy chủ. Kiểm tra API đã chạy chưa.';
    if (error.response.status === 403) return 'Bạn không có quyền thực hiện thao tác này.';
    if (error.response.status === 404) return 'Không tìm thấy dữ liệu.';
  }
  return 'Đã xảy ra lỗi. Vui lòng thử lại.';
}
