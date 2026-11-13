import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { App, Button, Card, Segmented, Space, Table, Tabs, Tooltip } from 'antd';
import { SyncOutlined } from '@ant-design/icons';
import { billingApi } from '../../api/billing';
import { extractErrorMessage } from '../../api/client';
import { odataString } from '../../api/odata';
import { PageTitle, StatusTag } from '../../components/Common';
import { dateTime, money } from '../../utils/format';
import type { PaymentTransaction, WebhookLog } from '../../types';

/**
 * UC-96: đối soát PayOS. Giao dịch online + nhật ký mọi báo có (webhook hoặc đồng bộ):
 * APPLIED đã ghi tiền; UNMATCHED/REJECTED/SURPLUS cần kế toán xử lý tay (hoàn tiền, gán hóa đơn).
 */
export default function Payments() {
  const { message } = App.useApp();
  const [status, setStatus] = useState('Pending');
  const [transactions, setTransactions] = useState<PaymentTransaction[]>([]);
  const [logs, setLogs] = useState<WebhookLog[]>([]);

  const load = useCallback(async () => {
    try {
      const page = await billingApi.transactions({ filter: status === 'All' ? [] : [`status eq ${odataString(status)}`], orderBy: 'id desc', top: 100 });
      setTransactions(page.items);
      setLogs(await billingApi.webhookLogs());
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  }, [status, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const sync = async (id: number) => {
    try {
      const result = await billingApi.syncPayment(id);
      message.info(`Giao dịch #${id}: ${result.status}`);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <>
      <PageTitle title="Đối soát PayOS" extra={<Button icon={<SyncOutlined />} onClick={() => void load()}>Tải lại</Button>} />
      <Card>
        <Tabs items={[
          {
            key: 'tx', label: 'Giao dịch', children: (
              <>
                <Segmented style={{ marginBottom: 12 }} value={status} onChange={(v) => setStatus(String(v))}
                  options={[{ value: 'Pending', label: 'Đang chờ' }, { value: 'Paid', label: 'Đã trả' }, { value: 'Expired', label: 'Hết hạn' }, { value: 'All', label: 'Tất cả' }]} />
                <Table rowKey="id" size="small" dataSource={transactions} pagination={{ pageSize: 15 }} columns={[
                  { title: '#', dataIndex: 'id', width: 60 },
                  { title: 'Hóa đơn', render: (_, t) => <Link to={`/staff/invoices/${t.invoiceId}`}>{t.invoiceNumber}</Link> },
                  { title: 'Cư dân', dataIndex: 'residentName' },
                  { title: 'Số tiền', dataIndex: 'amount', align: 'right', render: money },
                  { title: 'orderCode', dataIndex: 'orderCode' },
                  { title: 'Tạo lúc', dataIndex: 'createdAt', render: dateTime },
                  { title: 'Trả lúc', dataIndex: 'paidAt', render: dateTime },
                  { title: 'Trạng thái', dataIndex: 'status', render: (v: string) => <StatusTag kind="transaction" value={v} /> },
                  {
                    title: '', key: 's', render: (_, t) => t.status === 'Pending' && (
                      <Tooltip title="Hỏi payOS trạng thái ngay (khi webhook chưa tới)"><Button size="small" onClick={() => void sync(t.id)}>Đồng bộ</Button></Tooltip>
                    ),
                  },
                ]} />
              </>
            ),
          },
          {
            key: 'log', label: 'Nhật ký báo có', children: (
              <Table rowKey="id" size="small" dataSource={logs} pagination={{ pageSize: 15 }} columns={[
                { title: 'Lúc', dataIndex: 'receivedAt', render: dateTime },
                { title: 'Nguồn', dataIndex: 'source' },
                { title: 'Kết quả', dataIndex: 'outcome', render: (v: string) => <StatusTag kind="outcome" value={v} /> },
                { title: 'orderCode', dataIndex: 'orderCode' },
                { title: 'Số tiền', dataIndex: 'amount', align: 'right', render: money },
                { title: 'Mã NH', dataIndex: 'providerEventId' },
                { title: 'Chữ ký', dataIndex: 'signatureValid', render: (v: boolean) => (v ? 'Hợp lệ' : 'Sai') },
                { title: 'Lý do', dataIndex: 'reason' },
              ]} />
            ),
          },
        ]} />
      </Card>
      <Space style={{ marginTop: 12 }}><small>Local: chạy <code>node scripts/payos-fake/server.mjs</code> rồi mở http://localhost:5199/dev để giả lập khách trả tiền.</small></Space>
    </>
  );
}
