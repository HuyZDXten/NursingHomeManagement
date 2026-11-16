// BL-V1, UC-02 (chép từ ASS2): Google Identity Services — "Sign in with Google".
// Script chỉ tải khi trang đăng nhập cần và máy chủ đã cấu hình Client ID (GET /api/auth/config).

interface GoogleCredentialResponse {
  /** ID token Google ký, API kiểm lại. */
  credential?: string;
}

interface GoogleButtonOptions {
  type?: 'standard' | 'icon';
  theme?: 'outline' | 'filled_blue' | 'filled_black';
  size?: 'large' | 'medium' | 'small';
  text?: 'signin_with' | 'signup_with' | 'continue_with' | 'signin';
  shape?: 'rectangular' | 'pill';
  locale?: string;
  /** Pixel, 200–400. */
  width?: number;
}

export interface GoogleAccountsId {
  initialize: (config: { client_id: string; callback: (response: GoogleCredentialResponse) => void }) => void;
  renderButton: (parent: HTMLElement, options: GoogleButtonOptions) => void;
}

declare global {
  interface Window {
    google?: { accounts: { id: GoogleAccountsId } };
  }
}

let scriptPromise: Promise<GoogleAccountsId> | null = null;

export function loadGoogleIdentity(): Promise<GoogleAccountsId> {
  scriptPromise ??= new Promise<GoogleAccountsId>((resolve, reject) => {
    const ready = () => {
      const id = window.google?.accounts.id;
      if (id) resolve(id);
      else reject(new Error('Không tải được đăng nhập Google.'));
    };
    if (window.google?.accounts.id) {
      ready();
      return;
    }

    const script = document.createElement('script');
    script.src = 'https://accounts.google.com/gsi/client';
    script.async = true;
    script.onload = ready;
    script.onerror = () => {
      scriptPromise = null; // cho thử lại khi có mạng
      reject(new Error('Không kết nối được Google. Kiểm tra mạng rồi tải lại trang.'));
    };
    document.head.appendChild(script);
  });
  return scriptPromise;
}
