import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, App, DatePicker, Form, Modal, Table, Tabs, Typography } from 'antd';
import type { Dayjs } from 'dayjs';
import { billingExtrasApi } from '../../api/billingExtras';
import { extractErrorMessage } from '../../api/client';
import { dayjs, money, monthOf, todayVn } from '../../utils/format';
import type { BatchInvoiceResult } from '../../types/operations';

/** UC-98: tạo hóa đơn Draft cho mọi cư dân có gói trong kỳ và chưa có hóa đơn; hiện danh sách đã tạo / bỏ qua kèm lý do. */
export function BatchInvoiceModal({ onClose, onDone }: { onClose: () => void; onDone: () => void }) {
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const [running, setRunning] = useState(false);
  const [result, setResult] = useState<BatchInvoiceResult>();

  const run = async () => {
    const values = await form.validateFields();
    setRunning(true);
    try {
      const outcome = await billingExtrasApi.batch(
        (values.period as Dayjs).startOf('month').format('YYYY-MM-DD'),
        (values.dueDate as Dayjs).format('YYYY-MM-DD'));
      setResult(outcome);
      message.success(`Đã tạo ${outcome.created.length} hóa đơn nháp, bỏ qua ${outcome.skipped.length} cư dân.`);
      onDone();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setRunning(false);
    }
  };

  const today = dayjs(todayVn());
  return (
    <Modal open width={760} title="Tạo hóa đơn hàng loạt" onCancel={onClose}
      okText={result ? 'Đóng' : 'Tạo hóa đơn'} onOk={result ? onClose : run} confirmLoading={running}
      cancelButtonProps={{ style: result ? { display: 'none' } : undefined }}>
      {!result ? (
        <>
          <Alert type="info" showIcon style={{ marginBottom: 16 }}
            message="Tạo hóa đơn nháp (Draft) cho mọi cư dân có gói trong kỳ và chưa có hóa đơn kỳ đó. Dịch vụ phát sinh chưa tính tiền được thêm tự động. Kiểm tra rồi phát hành từng hóa đơn." />
          <Form form={form} layout="vertical" initialValues={{ period: today.subtract(1, 'month'), dueDate: today.date(10).isAfter(today) ? today.date(10) : today.add(7, 'day') }}>
            <Form.Item name="period" label="Kỳ tính phí" rules={[{ required: true }]}><DatePicker picker="month" format="MM/YYYY" style={{ width: '100%' }} /></Form.Item>
            <Form.Item name="dueDate" label="Hạn thanh toán" rules={[{ required: true }]}><DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} /></Form.Item>
          </Form>
        </>
      ) : (
        <>
          <Typography.Paragraph>Kỳ {monthOf(result.billingPeriod)}:</Typography.Paragraph>
          <Tabs items={[
            {
              key: 'created', label: `Đã tạo (${result.created.length})`, children: (
                <Table rowKey="id" size="small" dataSource={result.created} pagination={false} columns={[
                  { title: 'Số', render: (_, i) => <Link to={`/staff/invoices/${i.id}`} onClick={onClose}>{i.number}</Link> },
                  { title: 'Cư dân', render: (_, i) => `${i.residentCode} · ${i.residentName}` },
                  { title: 'Tổng', dataIndex: 'total', align: 'right', render: money },
                ]} />
              ),
            },
            {
              key: 'skipped', label: `Bỏ qua (${result.skipped.length})`, children: (
                <Table rowKey="residentId" size="small" dataSource={result.skipped} pagination={false} columns={[
                  { title: 'Cư dân', render: (_, s) => `${s.residentCode} · ${s.residentName}` },
                  { title: 'Lý do', dataIndex: 'reason' },
                ]} />
              ),
            },
          ]} />
        </>
      )}
    </Modal>
  );
}
