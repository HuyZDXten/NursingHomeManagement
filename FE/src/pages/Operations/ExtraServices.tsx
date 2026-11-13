import { useCallback, useEffect, useState } from 'react';
import { App, Button, Form, Input, InputNumber, Modal, Switch, Table, Tag, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { billingExtrasApi } from '../../api/billingExtras';
import { extractErrorMessage } from '../../api/client';
import { PageTitle } from '../../components/Common';
import { money } from '../../utils/format';
import type { ExtraService, ExtraServiceForm } from '../../types/operations';

const vnd = {
  formatter: (v?: string | number) => `${v ?? ''}`.replace(/\B(?=(\d{3})+(?!\d))/g, '.'),
  parser: (v?: string) => Number((v ?? '').replace(/\./g, '')),
};

/** UC-85 (Admin): danh mục dịch vụ phát sinh. Đổi giá không ảnh hưởng lần sử dụng đã ghi (giá chốt lúc ghi). */
export default function ExtraServices() {
  const { message } = App.useApp();
  const [rows, setRows] = useState<ExtraService[]>([]);
  const [editing, setEditing] = useState<ExtraService | 'new' | null>(null);
  const [form] = Form.useForm<ExtraServiceForm>();

  const load = useCallback(() => billingExtrasApi.services().then(setRows).catch((e) => message.error(extractErrorMessage(e))), [message]);
  useEffect(() => {
    void load();
  }, [load]);

  const save = async () => {
    const values = await form.validateFields();
    try {
      if (editing === 'new') await billingExtrasApi.createService(values);
      else if (editing) await billingExtrasApi.updateService(editing.id, values);
      message.success('Đã lưu dịch vụ.');
      setEditing(null);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <>
      <PageTitle
        title="Dịch vụ phát sinh"
        extra={<Button type="primary" icon={<PlusOutlined />} onClick={() => { form.resetFields(); form.setFieldsValue({ isActive: true }); setEditing('new'); }}>Thêm dịch vụ</Button>}
      />
      <Typography.Paragraph type="secondary">
        Nhân viên ghi lần sử dụng ở trang "Ghi dịch vụ phát sinh"; khi tạo hóa đơn tháng, các lần chưa tính tiền được thêm thành dòng phí với đơn giá lúc ghi.
      </Typography.Paragraph>
      <Table
        rowKey="id"
        dataSource={rows}
        pagination={false}
        columns={[
          { title: 'Tên dịch vụ', dataIndex: 'name' },
          { title: 'Đơn vị', dataIndex: 'unit' },
          { title: 'Đơn giá', dataIndex: 'unitPrice', align: 'right', render: money },
          { title: 'Trạng thái', dataIndex: 'isActive', render: (v: boolean) => (v ? <Tag color="green">Đang cung cấp</Tag> : <Tag>Ngừng</Tag>) },
          { title: '', key: 'edit', render: (_, s) => <Button size="small" onClick={() => { form.setFieldsValue(s); setEditing(s); }}>Sửa</Button> },
        ]}
      />
      <Modal open={editing !== null} title={editing === 'new' ? 'Thêm dịch vụ' : 'Sửa dịch vụ'} onOk={save} onCancel={() => setEditing(null)} okText="Lưu" destroyOnHidden>
        <Form form={form} layout="vertical">
          <Form.Item name="name" label="Tên dịch vụ" rules={[{ required: true, max: 150 }]}><Input placeholder="Giặt ủi, vật lý trị liệu…" /></Form.Item>
          <Form.Item name="unit" label="Đơn vị tính" rules={[{ required: true, max: 30 }]}><Input placeholder="kg, buổi, lần, chuyến" /></Form.Item>
          <Form.Item name="unitPrice" label="Đơn giá (VND)" rules={[{ required: true }]}>
            <InputNumber<number> min={0} step={10000} style={{ width: '100%' }} {...vnd} />
          </Form.Item>
          <Form.Item name="isActive" label="Đang cung cấp" valuePropName="checked"><Switch /></Form.Item>
        </Form>
      </Modal>
    </>
  );
}
