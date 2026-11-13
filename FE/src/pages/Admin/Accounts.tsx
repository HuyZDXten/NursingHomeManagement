import { useCallback, useEffect, useState } from 'react';
import { App, Button, Form, Input, Modal, Select, Space, Switch, Table, Tag } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { accountsApi, type AccountForm } from '../../api/accounts';
import { extractErrorMessage } from '../../api/client';
import { containsAny, odataString } from '../../api/odata';
import { PageTitle } from '../../components/Common';
import { dateTime } from '../../utils/format';
import { ROLE_LABEL } from '../../utils/status';
import { PASSWORD_RULE } from '../Common/Profile';
import { ROLES, type Account, type Role } from '../../types';

const PAGE_SIZE = 10;
const roleOptions = ROLES.map((r) => ({ value: r, label: ROLE_LABEL[r] }));

/** UC-09..UC-12: Admin quản lý tài khoản (danh sách OData: lọc, tìm, phân trang phía server). */
export default function Accounts() {
  const { message } = App.useApp();
  const [rows, setRows] = useState<Account[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [keyword, setKeyword] = useState('');
  const [role, setRole] = useState<Role>();
  const [loading, setLoading] = useState(false);
  const [editing, setEditing] = useState<Account | 'new' | null>(null);
  const [form] = Form.useForm<AccountForm>();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await accountsApi.list({
        filter: [containsAny(['fullName', 'email'], keyword) ?? '', role ? `role eq ${odataString(role)}` : ''],
        orderBy: 'id', top: PAGE_SIZE, skip: (page - 1) * PAGE_SIZE,
      });
      setRows(result.items);
      setTotal(result.total);
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [keyword, role, page, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const open = (account: Account | 'new') => {
    setEditing(account);
    form.resetFields();
    if (account !== 'new') form.setFieldsValue(account);
    else form.setFieldsValue({ role: 'Caregiver', isActive: true });
  };

  const save = async () => {
    const values = await form.validateFields();
    try {
      if (editing === 'new') await accountsApi.create(values);
      else if (editing) await accountsApi.update(editing.id, values);
      message.success('Đã lưu tài khoản.');
      setEditing(null);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  const resetPassword = (account: Account) => {
    let value = '';
    Modal.confirm({
      title: `Đặt lại mật khẩu cho ${account.fullName}`,
      content: <Input.Password placeholder="Mật khẩu mới" onChange={(e) => { value = e.target.value; }} />,
      onOk: async () => {
        try {
          await accountsApi.resetPassword(account.id, value);
          message.success('Đã đặt lại mật khẩu, mọi phiên của tài khoản đã bị thu hồi.');
        } catch (e) {
          message.error(extractErrorMessage(e));
          throw e;
        }
      },
    });
  };

  return (
    <>
      <PageTitle title="Tài khoản" extra={<Button type="primary" icon={<PlusOutlined />} onClick={() => open('new')}>Thêm tài khoản</Button>} />
      <Space style={{ marginBottom: 16 }} wrap>
        <Input.Search placeholder="Tìm tên hoặc email" allowClear onSearch={(v) => { setPage(1); setKeyword(v); }} style={{ width: 260 }} />
        <Select placeholder="Vai trò" allowClear options={roleOptions} style={{ width: 160 }} onChange={(v) => { setPage(1); setRole(v); }} />
      </Space>
      <Table
        rowKey="id"
        loading={loading}
        dataSource={rows}
        pagination={{ current: page, pageSize: PAGE_SIZE, total, onChange: setPage, showTotal: (t) => `${t} tài khoản` }}
        columns={[
          { title: 'Họ tên', dataIndex: 'fullName' },
          { title: 'Email', dataIndex: 'email' },
          { title: 'Vai trò', dataIndex: 'role', render: (r: Role) => ROLE_LABEL[r] },
          { title: 'Trạng thái', dataIndex: 'isActive', render: (v: boolean) => (v ? <Tag color="green">Hoạt động</Tag> : <Tag>Đã khóa</Tag>) },
          { title: 'Tạo lúc', dataIndex: 'createdAt', render: dateTime },
          {
            title: '', key: 'actions', render: (_, a) => (
              <Space>
                <Button size="small" onClick={() => open(a)}>Sửa</Button>
                <Button size="small" onClick={() => resetPassword(a)}>Đặt lại mật khẩu</Button>
              </Space>
            ),
          },
        ]}
      />
      <Modal open={editing !== null} title={editing === 'new' ? 'Thêm tài khoản' : 'Sửa tài khoản'} onOk={save} onCancel={() => setEditing(null)} okText="Lưu" destroyOnHidden>
        <Form form={form} layout="vertical">
          {editing === 'new' && <Form.Item name="email" label="Email" rules={[{ required: true, type: 'email' }]}><Input /></Form.Item>}
          <Form.Item name="fullName" label="Họ tên" rules={[{ required: true, max: 150 }]}><Input /></Form.Item>
          <Form.Item name="phone" label="Số điện thoại"><Input /></Form.Item>
          <Form.Item name="role" label="Vai trò" rules={[{ required: true }]}><Select options={roleOptions} /></Form.Item>
          {editing === 'new'
            ? <Form.Item name="password" label="Mật khẩu ban đầu" rules={[PASSWORD_RULE]}><Input.Password /></Form.Item>
            : <Form.Item name="isActive" label="Đang hoạt động" valuePropName="checked"><Switch /></Form.Item>}
        </Form>
      </Modal>
    </>
  );
}
