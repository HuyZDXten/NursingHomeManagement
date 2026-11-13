import { useCallback, useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { Alert, Button, Card, Col, Descriptions, Row, Table } from 'antd';
import { billingApi } from '../../api/billing';
import { extractErrorMessage } from '../../api/client';
import { PageTitle, StatusTag } from '../../components/Common';
import { PayOsQrPanel } from '../../components/PayOsQrPanel';
import { dateOnly, dateTime, money, monthOf } from '../../utils/format';
import type { InvoiceDetail } from '../../types';

/** UC-93: gia đình xem hóa đơn và thanh toán bằng VietQR PayOS. */
export default function FamilyInvoice() {
  const id = Number(useParams().id);
  const [invoice, setInvoice] = useState<InvoiceDetail>();
  const [error, setError] = useState<string>();
  const [paying, setPaying] = useState(false);

  const load = useCallback(() => billingApi.invoice(id).then(setInvoice).catch((e) => setError(extractErrorMessage(e))), [id]);
  useEffect(() => {
    void load();
  }, [load]);

  if (error) return <Alert type="error" showIcon title={error} />;
  if (!invoice) return <Card loading />;
  const payable = (invoice.status === 'Unpaid' || invoice.status === 'PartiallyPaid') && invoice.remaining > 0;

  return (
    <>
      <PageTitle title={<>Hóa đơn {invoice.number} <StatusTag kind="invoice" value={invoice.status} /></>} />
      <Row gutter={16}>
        <Col xs={24} lg={14}>
          <Card style={{ marginBottom: 16 }}>
            <Descriptions column={1} size="small">
              <Descriptions.Item label="Người thân">{invoice.residentName}</Descriptions.Item>
              <Descriptions.Item label="Kỳ">{monthOf(invoice.billingPeriod)}</Descriptions.Item>
              <Descriptions.Item label="Hạn thanh toán">{dateOnly(invoice.dueDate)}</Descriptions.Item>
              <Descriptions.Item label="Tổng">{money(invoice.total)}</Descriptions.Item>
              <Descriptions.Item label="Đã thanh toán">{money(invoice.paid)}</Descriptions.Item>
              <Descriptions.Item label="Còn lại"><b>{money(invoice.remaining)}</b></Descriptions.Item>
            </Descriptions>
          </Card>
          <Card title="Chi tiết" style={{ marginBottom: 16 }}>
            <Table rowKey="id" size="small" dataSource={invoice.items} pagination={false} columns={[
              { title: 'Nội dung', dataIndex: 'description' },
              { title: 'Thành tiền', dataIndex: 'amount', align: 'right', render: money },
            ]} />
          </Card>
          {invoice.payments.length > 0 && (
            <Card title="Đã thanh toán">
              <Table rowKey="id" size="small" dataSource={invoice.payments} pagination={false} columns={[
                { title: 'Lúc', dataIndex: 'paidAt', render: dateTime },
                { title: 'Hình thức', dataIndex: 'method', render: (v: string) => <StatusTag kind="method" value={v} /> },
                { title: 'Số tiền', dataIndex: 'amount', align: 'right', render: money },
              ]} />
            </Card>
          )}
        </Col>
        <Col xs={24} lg={10}>
          {payable && (
            <Card title="Thanh toán online">
              {paying
                ? <PayOsQrPanel invoiceId={id} onPaid={() => void load()} />
                : <Button type="primary" size="large" block onClick={() => setPaying(true)}>Thanh toán {money(invoice.remaining)} bằng VietQR</Button>}
            </Card>
          )}
        </Col>
      </Row>
    </>
  );
}
