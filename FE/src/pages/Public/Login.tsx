import { useCallback, useEffect, useState } from 'react';
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { Alert, Button, Card, Divider, Form, Input, Typography } from 'antd';
import { useAuth } from '../../auth/AuthContext';
import { homePath } from '../../auth/roles';
import { authApi } from '../../api/auth';
import { extractErrorMessage } from '../../api/client';
import { GoogleSignInButton } from '../../components/GoogleSignInButton';
import type { Role } from '../../types';

const DEMO = ['admin', 'receptionist', 'accountant', 'doctor', 'nurse', 'caregiver', 'family', 'family2'];

export default function Login() {
  const { login, applySession } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [params] = useSearchParams();
  const [error, setError] = useState<string>();
  const [loading, setLoading] = useState(false);
  const [googleClientId, setGoogleClientId] = useState('');
  const [form] = Form.useForm();

  // BL-V1, UC-02: nút Google chỉ hiện khi máy chủ có Google:ClientId.
  useEffect(() => {
    authApi.config().then((c) => setGoogleClientId(c.googleClientId)).catch(() => undefined);
  }, []);

  const goHome = useCallback((role: Role) => {
    const from = (location.state as { from?: { pathname: string } } | null)?.from?.pathname;
    navigate(from ?? homePath(role), { replace: true });
  }, [location.state, navigate]);

  const submit = async (values: { email: string; password: string }) => {
    setLoading(true);
    setError(undefined);
    try {
      const user = await login(values.email, values.password);
      goHome(user.role);
    } catch (e) {
      setError(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  };

  const signInWithGoogle = useCallback(async (idToken: string) => {
    setLoading(true);
    setError(undefined);
    try {
      const session = await authApi.google(idToken);
      applySession(session);
      goHome(session.role);
    } catch (e) {
      setError(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [applySession, goHome]);

  return (
    <div className="auth-box">
      <Card>
        <Typography.Title level={3}>Đăng nhập</Typography.Title>
        {params.get('expired') && <Alert type="warning" showIcon title="Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại." style={{ marginBottom: 16 }} />}
        {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 16 }} />}
        <Form form={form} layout="vertical" onFinish={submit} requiredMark={false}>
          <Form.Item name="email" label="Email" rules={[{ required: true, type: 'email', message: 'Nhập email hợp lệ' }]}>
            <Input autoComplete="username" autoFocus />
          </Form.Item>
          <Form.Item name="password" label="Mật khẩu" rules={[{ required: true, message: 'Nhập mật khẩu' }]}>
            <Input.Password autoComplete="current-password" />
          </Form.Item>
          <Button type="primary" htmlType="submit" block loading={loading}>Đăng nhập</Button>
        </Form>
        {googleClientId && (
          <>
            <Divider plain>hoặc</Divider>
            <GoogleSignInButton clientId={googleClientId} onCredential={(token) => void signInWithGoogle(token)} />
            <Typography.Paragraph type="secondary" style={{ marginTop: 8, fontSize: 12, textAlign: 'center' }}>
              Chỉ dùng được khi email Google đã có tài khoản tại cơ sở.
            </Typography.Paragraph>
          </>
        )}
        <div style={{ marginTop: 12 }}><Link to="/forgot-password">Quên mật khẩu?</Link></div>
        {import.meta.env.DEV && (
          <Typography.Paragraph type="secondary" style={{ marginTop: 16, fontSize: 12 }}>
            Tài khoản demo (mật khẩu <code>Demo@123456</code>):{' '}
            {DEMO.map((name) => (
              <Button key={name} size="small" type="link" onClick={() => form.setFieldsValue({ email: `${name}@nursinghome.local`, password: 'Demo@123456' })}>
                {name}
              </Button>
            ))}
          </Typography.Paragraph>
        )}
      </Card>
    </div>
  );
}
