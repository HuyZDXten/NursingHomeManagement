import { useEffect, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { Alert, Button, Card, Descriptions, Form, Input, Typography } from 'antd';
import { extractErrorMessage } from '../../api/client';
import { invitationsApi } from '../../api/invitations';
import { useAuth } from '../../auth/AuthContext';
import { PASSWORD_RULE } from '../Common/Profile';
import { dateTime } from '../../utils/format';
import type { InvitationLookup } from '../../types/operations';

/** BL-V1, UC-08: trang công khai /kich-hoat?code=... — người thân đặt họ tên + mật khẩu, vào thẳng trang gia đình. */
export default function ActivateAccount() {
  const [params] = useSearchParams();
  const code = params.get('code')?.trim() ?? '';
  const { applySession } = useAuth();
  const navigate = useNavigate();
  const [info, setInfo] = useState<InvitationLookup>();
  const [error, setError] = useState<string | undefined>(code ? undefined : 'Thiếu mã mời. Hãy mở đúng link trong email của cơ sở.');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!code) return;
    invitationsApi.lookup(code).then(setInfo).catch((e) => setError(extractErrorMessage(e)));
  }, [code]);

  const submit = async (values: { fullName: string; phone?: string; password: string }) => {
    setSaving(true);
    setError(undefined);
    try {
      applySession(await invitationsApi.accept({ code, fullName: values.fullName, phone: values.phone, password: values.password }));
      navigate('/family', { replace: true });
    } catch (e) {
      setError(extractErrorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="auth-box">
      <Card>
        <Typography.Title level={3}>Kích hoạt tài khoản gia đình</Typography.Title>
        {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 16 }} />}
        {info && (
          <>
            <Descriptions column={1} size="small" style={{ marginBottom: 16 }}>
              <Descriptions.Item label="Cơ sở">{info.facilityName}</Descriptions.Item>
              <Descriptions.Item label="Người thân">{info.residentName} ({info.relationship})</Descriptions.Item>
              <Descriptions.Item label="Email đăng nhập">{info.email}</Descriptions.Item>
              <Descriptions.Item label="Hạn kích hoạt">{dateTime(info.expiresAt)}</Descriptions.Item>
            </Descriptions>
            <Form layout="vertical" onFinish={submit} requiredMark={false}>
              <Form.Item name="fullName" label="Họ tên của bạn" rules={[{ required: true, max: 150, message: 'Nhập họ tên' }]}>
                <Input autoComplete="name" autoFocus />
              </Form.Item>
              <Form.Item name="phone" label="Số điện thoại (không bắt buộc)" rules={[{ max: 20 }]}>
                <Input autoComplete="tel" />
              </Form.Item>
              <Form.Item name="password" label="Mật khẩu" rules={[PASSWORD_RULE]}>
                <Input.Password autoComplete="new-password" />
              </Form.Item>
              <Form.Item
                name="confirm"
                label="Nhập lại mật khẩu"
                dependencies={['password']}
                rules={[{ required: true, message: 'Nhập lại mật khẩu' }, ({ getFieldValue }) => ({
                  validator: (_, value) => (value === getFieldValue('password') ? Promise.resolve() : Promise.reject(new Error('Mật khẩu nhập lại không khớp'))),
                })]}
              >
                <Input.Password autoComplete="new-password" />
              </Form.Item>
              <Button type="primary" htmlType="submit" block loading={saving}>Kích hoạt và đăng nhập</Button>
            </Form>
          </>
        )}
        <div style={{ marginTop: 12 }}><Link to="/login">Đã có tài khoản? Đăng nhập</Link></div>
      </Card>
    </div>
  );
}
