import { useEffect } from 'react';
import { App, Button, Card, Col, Descriptions, Form, Input, Row } from 'antd';
import type { Rule } from 'antd/es/form';
import { authApi } from '../../api/auth';
import { extractErrorMessage } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { ROLE_LABEL } from '../../utils/status';

// Khớp PasswordRules.Pattern ở API.
// eslint-disable-next-line react-refresh/only-export-components
export const PASSWORD_RULE: Rule = {
  required: true,
  pattern: /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).{8,100}$/,
  message: 'Tối thiểu 8 ký tự, gồm chữ hoa, chữ thường, chữ số và ký hiệu.',
};

/** UC-06, UC-07: hồ sơ cá nhân và đổi mật khẩu. */
export default function Profile() {
  const { message } = App.useApp();
  const { user, applySession } = useAuth();
  const [profileForm] = Form.useForm();
  const [passwordForm] = Form.useForm();

  useEffect(() => {
    authApi.me().then((p) => profileForm.setFieldsValue(p)).catch(() => undefined);
  }, [profileForm]);

  const saveProfile = async (values: { fullName: string; phone?: string }) => {
    try {
      await authApi.updateMe(values);
      message.success('Đã lưu hồ sơ. Tên mới hiển thị sau lần làm mới phiên kế tiếp.');
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  const changePassword = async (values: { currentPassword: string; newPassword: string }) => {
    try {
      applySession(await authApi.changePassword(values.currentPassword, values.newPassword));
      passwordForm.resetFields();
      message.success('Đã đổi mật khẩu. Các thiết bị khác đã bị đăng xuất.');
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <Row gutter={[16, 16]}>
      <Col xs={24} lg={12}>
        <Card title="Hồ sơ cá nhân">
          <Descriptions column={1} size="small" style={{ marginBottom: 16 }}>
            <Descriptions.Item label="Email">{user?.email}</Descriptions.Item>
            <Descriptions.Item label="Vai trò">{user ? ROLE_LABEL[user.role] : ''}</Descriptions.Item>
          </Descriptions>
          <Form form={profileForm} layout="vertical" onFinish={saveProfile}>
            <Form.Item name="fullName" label="Họ tên" rules={[{ required: true, max: 150 }]}><Input /></Form.Item>
            <Form.Item name="phone" label="Số điện thoại"><Input /></Form.Item>
            <Button type="primary" htmlType="submit">Lưu</Button>
          </Form>
        </Card>
      </Col>
      <Col xs={24} lg={12}>
        <Card title="Đổi mật khẩu">
          <Form form={passwordForm} layout="vertical" onFinish={changePassword}>
            <Form.Item name="currentPassword" label="Mật khẩu hiện tại" rules={[{ required: true }]}><Input.Password /></Form.Item>
            <Form.Item name="newPassword" label="Mật khẩu mới" rules={[PASSWORD_RULE]}><Input.Password /></Form.Item>
            <Form.Item
              name="confirm"
              label="Nhập lại mật khẩu mới"
              dependencies={['newPassword']}
              rules={[{ required: true }, ({ getFieldValue }) => ({
                validator: (_, value) => (value === getFieldValue('newPassword') ? Promise.resolve() : Promise.reject(new Error('Mật khẩu nhập lại không khớp'))),
              })]}
            >
              <Input.Password />
            </Form.Item>
            <Button type="primary" htmlType="submit">Đổi mật khẩu</Button>
          </Form>
        </Card>
      </Col>
    </Row>
  );
}
