import { useCallback, useEffect, useState } from 'react';
import { App, Button, Form, Input, InputNumber, Modal, Switch, Table, Tag } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { billingApi, type PackageForm } from '../../api/billing';
import { extractErrorMessage } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { PageTitle } from '../../components/Common';
import { money } from '../../utils/format';
import type { ServicePackage } from '../../types';

/** UC-83: danh mục gói dịch vụ. Đổi giá không ảnh hưởng đăng ký đã chốt và hóa đơn cũ. */
export default function Packages() {
  const { message } = App.useApp();
  const { user } = useAuth();
  const isAdmin = user?.role === 'Admin';
  const [rows, setRows] = useState<ServicePackage[]>([]);
  const [editing, setEditing] = useState<ServicePackage | 'new' | null>(null);
  const [form] = Form.useForm<PackageForm>();

  const load = useCallback(() => billingApi.packages().then(setRows).catch((e) => message.error(extractErrorMessage(e))), [message]);
  useEffect(() => {
    void load();
  }, [load]);

  const save = async () => {
    const values = await form.validateFields();
    try {
      if (editing === 'new') await billingApi.createPackage(values);
      else if (editing) await billingApi.updatePackage(editing.id, values);
      message.success('Đã lưu gói dịch vụ.');
      setEditing(null);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <>
      <PageTitle
        title="Gói dịch vụ"
        extra={isAdmin && <Button type="primary" icon={<PlusOutlined />} onClick={() => { form.resetFields(); form.setFieldsValue({ isActive: true }); setEditing('new'); }}>Thêm gói</Button>}
      />
      <Table
        rowKey="id"
        dataSource={rows}
        pagination={false}
        columns={[
          { title: 'Tên gói', dataIndex: 'name' },
          { title: 'Giá/tháng', dataIndex: 'monthlyPrice', align: 'right', render: money },
          { title: 'Mô tả', dataIndex: 'description' },
          { title: 'Trạng thái', dataIndex: 'isActive', render: (v: boolean) => (v ? <Tag color="green">Đang bán</Tag> : <Tag>Ngừng</Tag>) },
          ...(isAdmin ? [{
            title: '', key: 'edit', render: (_: unknown, p: ServicePackage) => (
              <Button size="small" onClick={() => { form.setFieldsValue(p); setEditing(p); }}>Sửa</Button>
            ),
          }] : []),
        ]}
      />
      <Modal open={editing !== null} title={editing === 'new' ? 'Thêm gói' : 'Sửa gói'} onOk={save} onCancel={() => setEditing(null)} okText="Lưu" destroyOnHidden>
        <Form form={form} layout="vertical">
          <Form.Item name="name" label="Tên gói" rules={[{ required: true, max: 150 }]}><Input /></Form.Item>
          <Form.Item name="monthlyPrice" label="Giá/tháng (VND)" rules={[{ required: true }]}>
            <InputNumber<number> min={0} step={500000} style={{ width: '100%' }} formatter={(v) => `${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, '.')} parser={(v) => Number((v ?? '').replace(/\./g, ''))} />
          </Form.Item>
          <Form.Item name="description" label="Mô tả"><Input.TextArea rows={3} /></Form.Item>
          <Form.Item name="isActive" label="Đang bán" valuePropName="checked"><Switch /></Form.Item>
        </Form>
      </Modal>
    </>
  );
}
