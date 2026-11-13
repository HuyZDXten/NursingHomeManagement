import { useEffect, useRef, useState } from 'react';
import { Alert, Button, Descriptions, Result, Space, Spin, Typography } from 'antd';
import { QRCodeSVG } from 'qrcode.react';
import { billingApi } from '../api/billing';
import { extractErrorMessage } from '../api/client';
import { dateTime, money } from '../utils/format';
import type { Checkout, PaymentStatus } from '../types';

/**
 * UC-93/UC-94: hiện VietQR của PayOS ngay trên trang (không chuyển trang) và hỏi trạng thái mỗi 5 giây.
 * Hỏi trạng thái cũng là lớp 2: API tự hỏi payOS nếu webhook chưa tới.
 */
export function PayOsQrPanel({ invoiceId, onPaid }: { invoiceId: number; onPaid?: () => void }) {
  const [checkout, setCheckout] = useState<Checkout>();
  const [status, setStatus] = useState<PaymentStatus>();
  const [error, setError] = useState<string>();
  const paidNotified = useRef(false);

  useEffect(() => {
    billingApi.checkout(invoiceId).then(setCheckout).catch((e) => setError(extractErrorMessage(e)));
  }, [invoiceId]);

  useEffect(() => {
    if (!checkout) return;
    const timer = window.setInterval(async () => {
      try {
        const s = await billingApi.paymentStatus(checkout.transactionId);
        setStatus(s);
        if (s.status !== 'Pending') window.clearInterval(timer);
        if (s.status === 'Paid' && !paidNotified.current) {
          paidNotified.current = true;
          onPaid?.();
        }
      } catch {
        // Thử lại ở nhịp sau.
      }
    }, 5000);
    return () => window.clearInterval(timer);
  }, [checkout, onPaid]);

  if (error) return <Alert type="error" showIcon title={error} />;
  if (!checkout) return <Spin />;
  if (status?.status === 'Paid') {
    return <Result status="success" title="Thanh toán thành công" subTitle={`Đã nhận ${money(status.amount)} cho hóa đơn ${checkout.invoiceNumber}.`} />;
  }
  if (status && status.status !== 'Pending') {
    return <Result status="warning" title="Mã thanh toán không còn hiệu lực" subTitle="Đóng và mở lại để tạo mã mới." />;
  }

  return (
    <div style={{ textAlign: 'center' }}>
      <Typography.Paragraph>Mở ứng dụng ngân hàng, quét mã VietQR để chuyển khoản. Trang tự cập nhật khi nhận được tiền.</Typography.Paragraph>
      {checkout.qrCode && <QRCodeSVG value={checkout.qrCode} size={220} includeMargin />}
      <Descriptions column={1} size="small" bordered style={{ marginTop: 12, textAlign: 'left' }}>
        <Descriptions.Item label="Số tiền"><b>{money(checkout.amount)}</b></Descriptions.Item>
        <Descriptions.Item label="Ngân hàng">{checkout.bankName}</Descriptions.Item>
        <Descriptions.Item label="Số tài khoản">{checkout.accountNumber}</Descriptions.Item>
        <Descriptions.Item label="Chủ tài khoản">{checkout.accountName}</Descriptions.Item>
        <Descriptions.Item label="Nội dung">{checkout.description}</Descriptions.Item>
        <Descriptions.Item label="Hết hạn">{dateTime(checkout.expiresAt)}</Descriptions.Item>
      </Descriptions>
      <Space style={{ marginTop: 12 }}>
        <Spin size="small" /> <span>Đang chờ xác nhận từ ngân hàng...</span>
        {checkout.checkoutUrl && <Button type="link" href={checkout.checkoutUrl} target="_blank">Mở trang thanh toán PayOS</Button>}
      </Space>
    </div>
  );
}
