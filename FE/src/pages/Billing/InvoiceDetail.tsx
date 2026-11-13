import { useCallback, useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { App, Button, Card, Col, Descriptions, Form, Input, InputNumber, Modal, Popconfirm, Row, Select, Space, Table } from 'antd';
import { billingApi } from '../../api/billing';
import { extractErrorMessage } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { FINANCE_STAFF, hasRole } from '../../auth/roles';
import { PageTitle, StatusTag } from '../../components/Common';
import { PayOsQrPanel } from '../../components/PayOsQrPanel';
import { dateOnly, dateTime, money, monthOf } from '../../utils/format';
import type { InvoiceDetail as Invoice } from '../../types';

const vnd = {
  formatter: (v?: string | number) => `${v ?? ''}`.replace(/\B(?=(\d{3})+(?!\d))/g, '.'),
  parser: (v?: string) => Number((v ?? '').replace(/\./g, '')),
};

/** UC-89..UC-94: chi tiết hóa đơn, thêm phí khi Draft, phát hành, thu tiền, hủy, tạo VietQR tại quầy. */
export default function InvoiceDetail() {
  const id = Number(useParams().id);
  const { message } = App.useApp();
  const { user } = useAuth();
  const finance = hasRole(user?.role, FINANCE_STAFF);
  const [invoice, setInvoice] = useState<Invoice>();
  const [mode, setMode] = useState<'item' | 'pay' | 'qr' | null>(null);
  const [itemForm] = Form.useForm();
  const [payForm] = Form.useForm();

  const load = useCallback(() => billingApi.invoice(id).then(setInvoice).catch((e) => message.error(extractErrorMessage(e))), [id, message]);
  useEffect(() => {
    void load();
  }, [load]);

  const run = async (action: () => Promise<Invoice | unknown>, ok: string) => {
    try {
      const result = await action();
      if (result && typeof result === 'object' && 'items' in result) setInvoice(result as Invoice);
      else void load();
      message.success(ok);
      setMode(null);
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  if (!invoice) return <Card loading />;
  const payable = invoice.status === 'Unpaid' || invoice.status === 'PartiallyPaid';

  return (
    <>
      <PageTitle
        title={<>Hóa đơn {invoice.number} <StatusTag kind="invoice" value={invoice.status} /></>}
        extra={(
          <Space wrap>
            {invoice.status === 'Draft' && <Button onClick={() => { itemForm.resetFields(); itemForm.setFieldsValue({ quantity: 1 }); setMode('item'); }}>Thêm khoản phí</Button>}
            {invoice.status === 'Draft' && (
              <Popconfirm title="Phát hành sẽ khóa các khoản phí và báo cho gia đình. Tiếp tục?" onConfirm={() => run(() => billingApi.issue(id), 'Đã phát hành và gửi thông báo cho gia đình.')}>
                <Button type="primary">Phát hành</Button>
              </Popconfirm>
            )}
            {payable && <Button onClick={() => setMode('qr')}>Tạo mã VietQR</Button>}
            {payable && finance && <Button type="primary" onClick={() => { payForm.resetFields(); payForm.setFieldsValue({ amount: invoice.remaining, method: 'Cash' }); setMode('pay'); }}>Ghi nhận thanh toán</Button>}
            {finance && invoice.payments.length === 0 && invoice.status !== 'Cancelled' && (
              <Popconfirm title="Hủy hóa đơn? (kỳ này vẫn bị giữ để không tạo trùng)" onConfirm={() => run(() => billingApi.cancel(id), 'Đã hủy hóa đơn.')}>
                <Button danger>Hủy</Button>
              </Popconfirm>
            )}
          </Space>
        )}
      />
      <Row gutter={16}>
        <Col xs={24} lg={16}>
          <Card title="Các khoản phí" style={{ marginBottom: 16 }}>
            <Table rowKey="id" size="small" dataSource={invoice.items} pagination={false} columns={[
              { title: 'Nội dung', dataIndex: 'description' },
              { title: 'SL', dataIndex: 'quantity', align: 'right' },
              { title: 'Đơn giá', dataIndex: 'unitPrice', align: 'right', render: money },
              { title: 'Thành tiền', dataIndex: 'amount', align: 'right', render: money },
            ]} />
          </Card>
          <Card title="Thanh toán">
            <Table rowKey="id" size="small" dataSource={invoice.payments} pagination={false} columns={[
              { title: 'Lúc', dataIndex: 'paidAt', render: dateTime },
              { title: 'Hình thức', dataIndex: 'method', render: (v: string) => <StatusTag kind="method" value={v} /> },
              { title: 'Mã tham chiếu', dataIndex: 'reference' },
              { title: 'Số tiền', dataIndex: 'amount', align: 'right', render: money },
            ]} />
          </Card>
        </Col>
        <Col xs={24} lg={8}>
          <Card>
            <Descriptions column={1} size="small">
              <Descriptions.Item label="Cư dân">{invoice.residentCode} · {invoice.residentName}</Descriptions.Item>
              <Descriptions.Item label="Kỳ">{monthOf(invoice.billingPeriod)}</Descriptions.Item>
              <Descriptions.Item label="Hạn thanh toán">{dateOnly(invoice.dueDate)}</Descriptions.Item>
              <Descriptions.Item label="Phát hành">{dateTime(invoice.issuedAt) || '—'}</Descriptions.Item>
              <Descriptions.Item label="Tổng">{money(invoice.total)}</Descriptions.Item>
              <Descriptions.Item label="Đã thu">{money(invoice.paid)}</Descriptions.Item>
              <Descriptions.Item label="Còn nợ"><b>{money(invoice.remaining)}</b></Descriptions.Item>
            </Descriptions>
          </Card>
        </Col>
      </Row>

      <Modal open={mode === 'item'} title="Thêm khoản phí" onOk={async () => { const v = await itemForm.validateFields(); await run(() => billingApi.addItem(id, v), 'Đã thêm khoản phí.'); }} onCancel={() => setMode(null)} destroyOnHidden>
        <Form form={itemForm} layout="vertical">
          <Form.Item name="description" label="Nội dung" rules={[{ required: true, max: 250 }]}><Input placeholder="Giặt ủi, vật lý trị liệu..." /></Form.Item>
          <Space>
            <Form.Item name="quantity" label="Số lượng" rules={[{ required: true }]}><InputNumber min={0.01} step={1} /></Form.Item>
            <Form.Item name="unitPrice" label="Đơn giá (VND)" rules={[{ required: true }]}><InputNumber min={0} step={10000} style={{ width: 180 }} {...vnd} /></Form.Item>
          </Space>
        </Form>
      </Modal>

      <Modal open={mode === 'pay'} title="Ghi nhận thanh toán thủ công" onOk={async () => { const v = await payForm.validateFields(); await run(() => billingApi.recordPayment(id, v), 'Đã ghi nhận thanh toán.'); }} onCancel={() => setMode(null)} destroyOnHidden>
        <Form form={payForm} layout="vertical">
          <Form.Item name="amount" label={`Số tiền (còn nợ ${money(invoice.remaining)})`} rules={[{ required: true }]}><InputNumber min={1} max={invoice.remaining} style={{ width: '100%' }} {...vnd} /></Form.Item>
          <Form.Item name="method" label="Hình thức" rules={[{ required: true }]}><Select options={[{ value: 'Cash', label: 'Tiền mặt' }, { value: 'BankTransfer', label: 'Chuyển khoản' }]} /></Form.Item>
          <Form.Item name="reference" label="Mã phiếu thu / mã giao dịch (duy nhất)" rules={[{ required: true, max: 100 }]}><Input /></Form.Item>
        </Form>
      </Modal>

      <Modal open={mode === 'qr'} title="Thanh toán VietQR (PayOS)" footer={null} onCancel={() => { setMode(null); void load(); }} destroyOnHidden>
        <PayOsQrPanel invoiceId={id} onPaid={() => void load()} />
      </Modal>
    </>
  );
}
