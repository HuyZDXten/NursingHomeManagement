import { useCallback, useEffect, useState } from 'react';
import { Alert, App, AutoComplete, Button, Checkbox, Form, Input, Modal, Popconfirm, Space, Table, Typography } from 'antd';
import { MailOutlined } from '@ant-design/icons';
import { extractErrorMessage } from '../../api/client';
import { invitationsApi } from '../../api/invitations';
import { StatusTag } from '../../components/Common';
import { dateTime } from '../../utils/format';
import type { FamilyInvitation, FamilyInvitationCreated, FamilyInvitationForm } from '../../types/operations';

const RELATIONSHIPS = ['Con trai', 'Con gái', 'Vợ', 'Chồng', 'Cháu', 'Anh/chị/em', 'Con dâu', 'Con rể', 'Người giám hộ'];

/**
 * BL-V1, UC-08: lễ tân mời người thân tạo tài khoản gia đình cho một cư dân.
 * Mã + link chỉ hiện một lần lúc tạo (máy chủ chỉ lưu bản băm); có SMTP thì đã gửi email.
 */
export function InvitationPanel({ residentId, active }: { residentId: number; active: boolean }) {
  const { message } = App.useApp();
  const [rows, setRows] = useState<FamilyInvitation[]>([]);
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [created, setCreated] = useState<FamilyInvitationCreated>();
  const [form] = Form.useForm<FamilyInvitationForm>();

  const load = useCallback(() => invitationsApi.list(residentId).then(setRows).catch(() => undefined), [residentId]);
  useEffect(() => {
    void load();
  }, [load]);

  const submit = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      setCreated(await invitationsApi.create({ ...values, residentId }));
      setOpen(false);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  const revoke = async (id: number) => {
    try {
      await invitationsApi.revoke(id);
      message.success('Đã thu hồi mã mời.');
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <>
      <Space style={{ marginBottom: 12 }} wrap>
        <Button icon={<MailOutlined />} disabled={!active} onClick={() => { form.resetFields(); setOpen(true); }}>
          Mời người thân tạo tài khoản
        </Button>
        <Typography.Text type="secondary">Người thân tự đặt mật khẩu qua link, tài khoản được liên kết sẵn với cư dân.</Typography.Text>
      </Space>
      {rows.length > 0 && (
        <Table rowKey="id" size="small" dataSource={rows} pagination={false} style={{ marginBottom: 16 }} columns={[
          { title: 'Email được mời', dataIndex: 'email' },
          { title: 'Quan hệ', dataIndex: 'relationship' },
          {
            title: 'Trạng thái',
            render: (_, r) => <><StatusTag kind="invitation" value={r.status} />{r.accountName && <small> {r.accountName}</small>}</>,
          },
          { title: 'Hạn', render: (_, r) => dateTime(r.expiresAt) },
          { title: 'Người mời', dataIndex: 'createdByName' },
          {
            title: '', key: 'revoke', render: (_, r) => r.status === 'Pending' && (
              <Popconfirm title="Thu hồi mã mời? Link đã gửi sẽ không dùng được nữa." onConfirm={() => revoke(r.id)}>
                <Button size="small" danger>Thu hồi</Button>
              </Popconfirm>
            ),
          },
        ]} />
      )}

      <Modal open={open} title="Mời người thân tạo tài khoản" okText="Tạo mã mời" confirmLoading={saving} onOk={submit} onCancel={() => setOpen(false)} destroyOnHidden>
        <Form form={form} layout="vertical" initialValues={{ canViewHealth: true, canViewCare: true, canViewMedication: false, canViewBilling: true }}>
          <Form.Item name="email" label="Email người thân" rules={[{ required: true, type: 'email', message: 'Nhập email hợp lệ' }]}>
            <Input placeholder="dùng để đăng nhập" />
          </Form.Item>
          <Form.Item name="relationship" label="Quan hệ với cư dân" rules={[{ required: true, max: 50, message: 'Nhập quan hệ' }]}>
            <AutoComplete options={RELATIONSHIPS.map((value) => ({ value }))} />
          </Form.Item>
          <Form.Item name="canViewHealth" valuePropName="checked" style={{ marginBottom: 4 }}><Checkbox>Xem sức khỏe</Checkbox></Form.Item>
          <Form.Item name="canViewCare" valuePropName="checked" style={{ marginBottom: 4 }}><Checkbox>Xem chăm sóc</Checkbox></Form.Item>
          <Form.Item name="canViewMedication" valuePropName="checked" style={{ marginBottom: 4 }}><Checkbox>Xem thuốc</Checkbox></Form.Item>
          <Form.Item name="canViewBilling" valuePropName="checked"><Checkbox>Xem & thanh toán hóa đơn</Checkbox></Form.Item>
        </Form>
      </Modal>

      <Modal open={!!created} title="Đã tạo mã mời" footer={<Button type="primary" onClick={() => setCreated(undefined)}>Xong</Button>} onCancel={() => setCreated(undefined)}>
        {created && (
          <>
            {created.emailSent
              ? <Alert type="success" showIcon title={`Đã gửi link kích hoạt tới ${created.invitation.email}.`} style={{ marginBottom: 12 }} />
              : <Alert type="warning" showIcon title="Chưa gửi được email (máy chủ chưa cấu hình email hoặc gửi lỗi). Hãy gửi link dưới đây cho người thân." style={{ marginBottom: 12 }} />}
            <Typography.Paragraph strong style={{ marginBottom: 4 }}>Link kích hoạt</Typography.Paragraph>
            <Typography.Paragraph copyable={{ text: created.link }} style={{ wordBreak: 'break-all' }}>{created.link}</Typography.Paragraph>
            <Typography.Paragraph strong style={{ marginBottom: 4 }}>Mã mời</Typography.Paragraph>
            <Typography.Paragraph copyable code>{created.code}</Typography.Paragraph>
            <Typography.Paragraph type="secondary">
              Mã chỉ hiện một lần, dùng được một lần, hết hạn {dateTime(created.invitation.expiresAt)}. Mất mã thì tạo mã mới (mã cũ tự thu hồi).
            </Typography.Paragraph>
          </>
        )}
      </Modal>
    </>
  );
}
