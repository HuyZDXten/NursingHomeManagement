import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Button, Result, Spin } from 'antd';
import { billingApi } from '../../api/billing';
import type { PaymentStatus } from '../../types';

/**
 * Trang payOS chuyển về (PayOs:ReturnUrl / CancelUrl) khi người dùng thanh toán trên trang của payOS.
 * Query có orderCode; transactionId = orderCode / 1000. Trạng thái hỏi lại API (không tin query string).
 */
export default function PaymentResult() {
  const [params] = useSearchParams();
  const orderCode = Number(params.get('orderCode'));
  const [status, setStatus] = useState<PaymentStatus>();
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (!orderCode) {
      setFailed(true);
      return;
    }
    billingApi.paymentStatus(Math.floor(orderCode / 1000)).then(setStatus).catch(() => setFailed(true));
  }, [orderCode]);

  if (failed) return <Result status="warning" title="Không xác định được giao dịch" extra={<Button href="/family">Về trang gia đình</Button>} />;
  if (!status) return <Spin />;
  const back = <Button type="primary" href={`/family/invoices/${status.invoiceId}`}>Xem hóa đơn</Button>;
  if (status.status === 'Paid') return <Result status="success" title="Thanh toán thành công" extra={back} />;
  if (status.status === 'Pending') return <Result status="info" title="Đang chờ xác nhận từ ngân hàng" subTitle="Hệ thống tự cập nhật trong ít phút." extra={back} />;
  return <Result status="warning" title="Thanh toán chưa hoàn tất" subTitle={params.get('cancel') === 'true' ? 'Bạn đã hủy thanh toán.' : undefined} extra={back} />;
}
