import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { App, Button, DatePicker, Form, Input, Modal, Select, Space, Table } from 'antd';
import { BellOutlined, PlusOutlined, ThunderboltOutlined } from '@ant-design/icons';
import { billingApi } from '../../api/billing';
import { billingExtrasApi } from '../../api/billingExtras';
import { extractErrorMessage } from '../../api/client';
import { containsAny, odataString } from '../../api/odata';
import { useAuth } from '../../auth/AuthContext';
import { FINANCE_STAFF, hasRole } from '../../auth/roles';
import { PageTitle, ResidentSelect, StatusTag } from '../../components/Common';
import { dateOnly, dayjs, money, monthOf } from '../../utils/format';
import { statusOptions } from '../../utils/status';
import type { InvoiceListItem } from '../../types';
import { BatchInvoiceModal } from '../Operations/BatchInvoiceModal';

const PAGE_SIZE = 10;

/** UC-87: danh sách hóa đơn (OData) + tạo hóa đơn tháng tính theo ngày lưu trú. */
export default function Invoices() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const [rows, setRows] = useState<InvoiceListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [keyword, setKeyword] = useState('');
  const [status, setStatus] = useState<string>();
  const [creating, setCreating] = useState(false);
  const [batching, setBatching] = useState(false);
  const { user } = useAuth();
  const finance = hasRole(user?.role, FINANCE_STAFF);
  const [form] = Form.useForm();

  const load = useCallback(async () => {
    try {
      const result = await billingApi.invoices({
        filter: [containsAny(['number', 'residentName', 'residentCode'], keyword) ?? '', status ? `status eq ${odataString(status)}` : ''],
        orderBy: 'billingPeriod desc,id desc', top: PAGE_SIZE, skip: (page - 1) * PAGE_SIZE,
      });
      setRows(result.items);
      setTotal(result.total);
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  }, [keyword, status, page, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const create = async () => {
    const v = await form.validateFields();
    try {
      const invoice = await billingApi.createInvoice({
        residentId: v.residentId,
        billingPeriod: v.period.startOf('month').format('YYYY-MM-DD'),
        dueDate: v.dueDate.format('YYYY-MM-DD'),
      });
      navigate(`/staff/invoices/${invoice.id}`);
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  /** S3-V5 (UC-97): chạy nhắc nợ ngay — job nền cũng chạy mỗi giờ, mỗi mốc chỉ nhắc một lần. */
  const runReminders = async () => {
    try {
      const result = await billingExtrasApi.runReminders();
      message.success(result.sent ? `Đã gửi ${result.sent} lượt nhắc (xét ${result.checked} hóa đơn còn nợ).` : `Không có mốc nhắc mới (xét ${result.checked} hóa đơn còn nợ).`);
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <>
      <PageTitle
        title="Hóa đơn"
        extra={(
          <Space wrap>
            {finance && <Button icon={<BellOutlined />} onClick={runReminders}>Chạy nhắc nợ</Button>}
            <Button icon={<ThunderboltOutlined />} onClick={() => setBatching(true)}>Tạo hóa đơn hàng loạt</Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={() => { form.resetFields(); form.setFieldsValue({ period: dayjs().subtract(1, 'month'), dueDate: dayjs().date(10) }); setCreating(true); }}>Tạo hóa đơn tháng</Button>
          </Space>
        )}
      />
      {batching && <BatchInvoiceModal onClose={() => setBatching(false)} onDone={() => void load()} />}
      <Space style={{ marginBottom: 16 }} wrap>
        <Input.Search placeholder="Số hóa đơn, mã hoặc tên cư dân" allowClear onSearch={(v) => { setPage(1); setKeyword(v); }} style={{ width: 300 }} />
        <Select placeholder="Trạng thái" allowClear style={{ width: 180 }} options={statusOptions('invoice')} onChange={(v) => { setPage(1); setStatus(v); }} />
      </Space>
      <Table
        rowKey="id"
        dataSource={rows}
        onRow={(r) => ({ onClick: () => navigate(`/staff/invoices/${r.id}`), style: { cursor: 'pointer' } })}
        pagination={{ current: page, pageSize: PAGE_SIZE, total, onChange: setPage, showTotal: (t) => `${t} hóa đơn` }}
        columns={[
          { title: 'Số', dataIndex: 'number' },
          { title: 'Cư dân', render: (_, i) => `${i.residentCode} · ${i.residentName}` },
          { title: 'Kỳ', dataIndex: 'billingPeriod', render: monthOf },
          { title: 'Hạn', dataIndex: 'dueDate', render: dateOnly },
          { title: 'Tổng', dataIndex: 'total', align: 'right', render: money },
          { title: 'Còn nợ', dataIndex: 'remaining', align: 'right', render: money },
          { title: 'Trạng thái', dataIndex: 'status', render: (v: string) => <StatusTag kind="invoice" value={v} /> },
        ]}
      />
      <Modal open={creating} title="Tạo hóa đơn tháng" onOk={create} onCancel={() => setCreating(false)} okText="Tạo" destroyOnHidden>
        <Form form={form} layout="vertical">
          <Form.Item name="residentId" label="Cư dân" rules={[{ required: true }]}><ResidentSelect /></Form.Item>
          <Form.Item name="period" label="Kỳ tính phí" rules={[{ required: true }]}><DatePicker picker="month" format="MM/YYYY" style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="dueDate" label="Hạn thanh toán" rules={[{ required: true }]}><DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} /></Form.Item>
        </Form>
      </Modal>
    </>
  );
}
