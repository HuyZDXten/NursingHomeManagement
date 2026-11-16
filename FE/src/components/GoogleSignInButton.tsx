import { useEffect, useRef, useState } from 'react';
import { Typography } from 'antd';
import { loadGoogleIdentity } from '../auth/google';

interface GoogleSignInButtonProps {
  clientId: string;
  /** Nhận ID token Google sau khi người dùng chọn tài khoản. */
  onCredential: (idToken: string) => void;
}

/**
 * BL-V1, UC-02 (chép từ ASS2): nút của chính Google. Việc chọn tài khoản diễn ra trong iframe của Google,
 * trang không bao giờ thấy mật khẩu Google — chỉ nhận ID token đã ký để gửi lên API.
 */
export function GoogleSignInButton({ clientId, onCredential }: GoogleSignInButtonProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const callbackRef = useRef(onCredential);
  const [loadError, setLoadError] = useState<string>();

  // Google giữ callback từ initialize(); ref giúp luôn gọi đúng prop mới nhất.
  useEffect(() => {
    callbackRef.current = onCredential;
  }, [onCredential]);

  useEffect(() => {
    let cancelled = false;
    loadGoogleIdentity()
      .then((google) => {
        const container = containerRef.current;
        if (cancelled || !container) return;
        google.initialize({
          client_id: clientId,
          callback: (response) => {
            if (response.credential) callbackRef.current(response.credential);
          },
        });
        google.renderButton(container, {
          theme: 'outline',
          size: 'large',
          text: 'signin_with',
          shape: 'rectangular',
          locale: 'vi',
          width: Math.min(400, Math.max(200, container.offsetWidth)),
        });
      })
      .catch((err: Error) => {
        if (!cancelled) setLoadError(err.message);
      });
    return () => {
      cancelled = true;
    };
  }, [clientId]);

  if (loadError) return <Typography.Text type="danger">{loadError}</Typography.Text>;
  return <div ref={containerRef} style={{ width: '100%', display: 'flex', justifyContent: 'center' }} />;
}
