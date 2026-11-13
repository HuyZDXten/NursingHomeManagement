import { useState } from 'react';
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { Alert, Button, Card, Form, Input, Typography } from 'antd';
import { useAuth } from '../../auth/AuthContext';
import { homePath } from '../../auth/roles';
import { extractErrorMessage } from '../../api/client';

const DEMO = ['admin', 'receptionist', 'accountant', 'doctor', 'nurse', 'caregiver', 'family', 'family2'];

export default function Login() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [params] = useSearchParams();
  const [error, setError] = useState<string>();
  const [loading, setLoading] = useState(false);
  const [form] = Form.useForm();

  const submit = async (values: { email: string; password: string }) => {
    setLoading(true);
    setError(undefined);
    try {
      const user = await login(values.email, values.password);
      const from = (location.state as { from?: { pathname: string } } | null)?.from?.pathname;
      navigate(from ?? homePath(user.role), { replace: true });
    } catch (e) {
      setError(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  };

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
