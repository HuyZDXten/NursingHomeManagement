import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { App, Button, Card, Form, Input, Typography } from 'antd';
import { authApi } from '../../api/auth';
import { extractErrorMessage } from '../../api/client';
import { PASSWORD_RULE } from '../Common/Profile';

/** UC-05: nhận OTP qua email rồi đặt mật khẩu mới. */
export default function ForgotPassword() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const [email, setEmail] = useState<string>();
  const [loading, setLoading] = useState(false);

  const requestOtp = async (values: { email: string }) => {
    setLoading(true);
    try {
      const result = await authApi.forgotPassword(values.email);
      message.success(result.message);
      setEmail(values.email);
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  };

  const reset = async (values: { otp: string; newPassword: string }) => {
    setLoading(true);
    try {
      const result = await authApi.resetPassword(email!, values.otp, values.newPassword);
      message.success(result.message);
      navigate('/login');
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-box">
      <Card>
        <Typography.Title level={3}>Quên mật khẩu</Typography.Title>
        {!email ? (
          <Form layout="vertical" onFinish={requestOtp}>
            <Form.Item name="email" label="Email tài khoản" rules={[{ required: true, type: 'email' }]}><Input /></Form.Item>
            <Button type="primary" htmlType="submit" block loading={loading}>Gửi mã OTP</Button>
          </Form>
        ) : (
          <Form layout="vertical" onFinish={reset}>
            <Typography.Paragraph>Mã 6 số đã gửi tới <b>{email}</b> (hiệu lực 10 phút).</Typography.Paragraph>
            <Form.Item name="otp" label="Mã OTP" rules={[{ required: true, pattern: /^\d{6}$/, message: 'Mã gồm 6 chữ số' }]}><Input maxLength={6} /></Form.Item>
            <Form.Item name="newPassword" label="Mật khẩu mới" rules={[PASSWORD_RULE]}><Input.Password /></Form.Item>
            <Button type="primary" htmlType="submit" block loading={loading}>Đặt lại mật khẩu</Button>
          </Form>
        )}
        <div style={{ marginTop: 12 }}><Link to="/login">Quay lại đăng nhập</Link></div>
      </Card>
    </div>
  );
}
