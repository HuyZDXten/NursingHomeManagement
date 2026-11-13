import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, App, Button, Card, Col, DatePicker, Form, Input, InputNumber, Radio, Result, Row, Typography } from 'antd';
import type { Dayjs } from 'dayjs';
import { admissionsApi } from '../../api/admissions';
import { extractErrorMessage } from '../../api/client';
import { dayjs, todayVn } from '../../utils/format';
import { statusMeta } from '../../utils/status';
import type { TourSlot, TourSlotAvailability } from '../../types/operations';
import { PHONE_RULE } from './InquiryForm';

const MAX_DAYS_AHEAD = 60;
const SLOTS: TourSlot[] = ['Morning', 'Afternoon'];

interface Values {
  fullName: string;
  phone: string;
  email?: string;
  visitDate: Dayjs;
  timeSlot: TourSlot;
  guestCount: number;
  note?: string;
}

/** UC-18: đặt lịch tham quan công khai — chọn ngày (mai → +60 ngày), buổi, số khách; thấy số chỗ còn lại. */
export default function TourBooking() {
  const { message } = App.useApp();
  const [form] = Form.useForm<Values>();
  const [slots, setSlots] = useState<TourSlotAvailability[]>([]);
  const [loading, setLoading] = useState(false);
  const [done, setDone] = useState<string>();
  const visitDate = Form.useWatch('visitDate', form);
  const timeSlot = Form.useWatch('timeSlot', form);

  const first = useMemo(() => dayjs(todayVn()).add(1, 'day'), []);
  const last = useMemo(() => dayjs(todayVn()).add(MAX_DAYS_AHEAD, 'day'), []);

  const loadSlots = useCallback(async () => {
    try {
      setSlots(await admissionsApi.tourSlots(first.format('YYYY-MM-DD'), last.format('YYYY-MM-DD')));
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  }, [first, last, message]);

  useEffect(() => {
    void loadSlots();
  }, [loadSlots]);

  const remainingOf = useCallback((date: Dayjs | undefined, slot: TourSlot) => {
    if (!date) return undefined;
    const key = date.format('YYYY-MM-DD');
    return slots.find((s) => s.date === key && s.slot === slot)?.remaining;
  }, [slots]);

  const isFull = (date: Dayjs) => slots.length > 0 && SLOTS.every((slot) => (remainingOf(date, slot) ?? 0) === 0);
  const remaining = timeSlot ? remainingOf(visitDate, timeSlot) : undefined;

  const submit = async (values: Values) => {
    setLoading(true);
    try {
      const result = await admissionsApi.bookTour({ ...values, visitDate: values.visitDate.format('YYYY-MM-DD') });
      setDone(result.message);
      form.resetFields();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
      void loadSlots();
    }
  };

  if (done) {
    return (
      <div className="container">
        <Card>
          <Result status="success" title="Đã nhận lịch tham quan" subTitle={done}
            extra={[
              <Button key="again" onClick={() => setDone(undefined)}>Đặt lịch khác</Button>,
              <Link key="home" to="/"><Button type="primary">Về trang chủ</Button></Link>,
            ]} />
        </Card>
      </div>
    );
  }

  return (
    <div className="container" style={{ maxWidth: 760 }}>
      <Typography.Title level={2}>Đặt lịch tham quan</Typography.Title>
      <Typography.Paragraph type="secondary">
        Gia đình đến xem phòng, gặp đội ngũ chăm sóc và được tư vấn trực tiếp. Lễ tân sẽ gọi điện xác nhận lịch.
      </Typography.Paragraph>
      <Card>
        <Form form={form} layout="vertical" onFinish={submit} initialValues={{ guestCount: 1 }} requiredMark="optional">
          <Row gutter={16}>
            <Col xs={24} md={12}>
              <Form.Item name="visitDate" label="Ngày tham quan" rules={[{ required: true, message: 'Chọn ngày' }]}>
                <DatePicker
                  format="dddd, DD/MM/YYYY"
                  style={{ width: '100%' }}
                  disabledDate={(d) => d.isBefore(first, 'day') || d.isAfter(last, 'day') || isFull(d)}
                  onChange={() => form.setFieldValue('timeSlot', undefined)}
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={12}>
              <Form.Item name="guestCount" label="Số khách" rules={[{ required: true }]}
                extra={remaining !== undefined ? `Buổi đã chọn còn ${remaining} chỗ` : undefined}>
                <InputNumber<number> min={1} max={Math.max(1, Math.min(10, remaining ?? 10))} style={{ width: '100%' }} />
              </Form.Item>
            </Col>
            <Col span={24}>
              <Form.Item name="timeSlot" label="Buổi" rules={[{ required: true, message: 'Chọn buổi' }]}>
                <Radio.Group disabled={!visitDate}>
                  {SLOTS.map((slot) => {
                    const left = remainingOf(visitDate, slot);
                    return (
                      <Radio.Button key={slot} value={slot} disabled={left === 0}>
                        {statusMeta('tourSlot', slot).label}{left !== undefined ? ` · còn ${left} chỗ` : ''}
                      </Radio.Button>
                    );
                  })}
                </Radio.Group>
              </Form.Item>
            </Col>
            <Col xs={24} md={12}>
              <Form.Item name="fullName" label="Họ tên người liên hệ" rules={[{ required: true, message: 'Nhập họ tên' }, { max: 150 }]}>
                <Input autoComplete="name" />
              </Form.Item>
            </Col>
            <Col xs={24} md={12}>
              <Form.Item name="phone" label="Số điện thoại" rules={[{ required: true, message: 'Nhập số điện thoại' }, PHONE_RULE]}>
                <Input autoComplete="tel" inputMode="tel" />
              </Form.Item>
            </Col>
            <Col span={24}>
              <Form.Item name="email" label="Email (nhận xác nhận lịch)" rules={[{ type: 'email', message: 'Email không hợp lệ' }, { max: 256 }]}>
                <Input autoComplete="email" />
              </Form.Item>
            </Col>
            <Col span={24}>
              <Form.Item name="note" label="Ghi chú" rules={[{ max: 500 }]}>
                <Input.TextArea rows={2} maxLength={500} placeholder="Ví dụ: muốn xem phòng đơn, cần chỗ đỗ xe lăn..." />
              </Form.Item>
            </Col>
          </Row>
          {remaining === 0 && <Alert type="warning" showIcon message="Buổi này đã kín chỗ, vui lòng chọn buổi khác." style={{ marginBottom: 16 }} />}
          <Button type="primary" htmlType="submit" loading={loading} disabled={remaining === 0}>Gửi đăng ký</Button>
        </Form>
      </Card>
    </div>
  );
}
