import { useCallback, useEffect, useState } from 'react';
import { App, Button, Card, Col, DatePicker, Form, Input, InputNumber, Popconfirm, Row, Select, Space, Table, Tabs, Tag, Typography } from 'antd';
import { CalendarOutlined } from '@ant-design/icons';
import { extractErrorMessage } from '../../api/client';
import { portalApi } from '../../api/portal';
import { visitsApi } from '../../api/visits';
import { PageTitle, StatusTag } from '../../components/Common';
import { dateOnly, dayjs, hhmm } from '../../utils/format';
import type { FamilyResident } from '../../types';
import type { VisitBooking, VisitSlot } from '../../types/care';

/** S3-H3 (UC-103): gia đình đặt và quản lý lịch thăm. */
export default function FamilyVisits() {
  const { message } = App.useApp();
  const [residents, setResidents] = useState<FamilyResident[]>([]);
  const [slots, setSlots] = useState<VisitSlot[]>([]);
  const [bookings, setBookings] = useState<VisitBooking[]>([]);
  const [selectedDate, setSelectedDate] = useState<dayjs.Dayjs>(dayjs().add(1, 'day'));
  const [selectedSlot, setSelectedSlot] = useState<string>();
  const [submitting, setSubmitting] = useState(false);
  const [form] = Form.useForm();

  const loadBookings = useCallback(() => {
    visitsApi.mine().then(setBookings).catch(() => undefined);
  }, []);

  const loadSlots = useCallback((d: dayjs.Dayjs) => {
    setSelectedSlot(undefined);
    visitsApi.slots(d.format('YYYY-MM-DD')).then(setSlots).catch(() => setSlots([]));
  }, []);

  useEffect(() => {
    portalApi.familyResidents().then((list) => {
      const active = list.filter((r) => r.status === 'Active');
      setResidents(active);
      if (active.length === 1) {
        form.setFieldValue('residentId', active[0].residentId);
      }
    }).catch(() => undefined);
    loadBookings();
  }, [form, loadBookings]);

  useEffect(() => {
    loadSlots(selectedDate);
  }, [selectedDate, loadSlots]);

  const submit = async () => {
    const v = await form.validateFields();
    if (!selectedSlot) {
      message.warning('Vui lòng chọn khung giờ thăm.');
      return;
    }
    setSubmitting(true);
    try {
      await visitsApi.book({
        residentId: v.residentId,
        visitDate: selectedDate.format('YYYY-MM-DD'),
        slotStart: selectedSlot,
        guestCount: v.guestCount,
        guestNames: v.guestNames,
        note: v.note,
      });
      message.success('Đã gửi yêu cầu đặt lịch thăm. Lễ tân sẽ xác nhận sớm.');
      form.resetFields(['guestNames', 'note']);
      loadBookings();
      loadSlots(selectedDate);
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSubmitting(false);
    }
  };

  const todayStr = dayjs().format('YYYY-MM-DD');
  const upcoming = bookings.filter(
    (b) => b.visitDate >= todayStr && ['Requested', 'Approved', 'CheckedIn'].includes(b.status),
  );
  const history = bookings.filter((b) => !upcoming.some((u) => u.id === b.id));

  return (
    <>
      <PageTitle title="Đặt lịch thăm người thân" />
      <Row gutter={[16, 16]}>
        <Col xs={24} lg={10}>
          <Card title={<><CalendarOutlined /> Đăng ký lịch thăm</>}>
            <Form form={form} layout="vertical" initialValues={{ guestCount: 1 }}>
              <Form.Item name="residentId" label="Người thân" rules={[{ required: true, message: 'Chọn người thân' }]}>
                <Select
                  placeholder="Chọn người thân"
                  options={residents.map((r) => ({ value: r.residentId, label: `${r.code} · ${r.fullName}` }))}
                />
              </Form.Item>
              <Form.Item label="Ngày thăm">
                <DatePicker
                  value={selectedDate}
                  onChange={(d) => d && setSelectedDate(d)}
                  format="DD/MM/YYYY"
                  allowClear={false}
                  style={{ width: '100%' }}
                  disabledDate={(current) =>
                    current < dayjs().startOf('day') || current > dayjs().add(30, 'day').endOf('day')
                  }
                />
              </Form.Item>
              <Form.Item label="Chọn khung giờ (1 giờ / lượt)" required>
                <Space wrap>
                  {slots.map((s) => {
                    const label = `${hhmm(s.start)}–${hhmm(s.end)}`;
                    const disabled = s.isPast || s.remaining <= 0;
                    const active = selectedSlot === s.start;
                    return (
                      <Button
                        key={s.start}
                        type={active ? 'primary' : 'default'}
                        disabled={disabled}
                        onClick={() => setSelectedSlot(s.start)}
                      >
                        {label} <Tag style={{ marginLeft: 4 }} color={s.remaining > 0 && !s.isPast ? 'green' : 'default'}>còn {s.remaining}</Tag>
                      </Button>
                    );
                  })}
                </Space>
                {slots.length === 0 && <Typography.Text type="secondary">Không có khung giờ khả dụng.</Typography.Text>}
              </Form.Item>
              <Form.Item name="guestCount" label="Số khách (1–5)" rules={[{ required: true }]}>
                <InputNumber min={1} max={5} style={{ width: '100%' }} />
              </Form.Item>
              <Form.Item name="guestNames" label="Tên khách đi cùng">
                <Input maxLength={500} placeholder="Ví dụ: Nguyễn Thị Bích, Nguyễn Văn An" />
              </Form.Item>
              <Form.Item name="note" label="Ghi chú cho lễ tân">
                <Input.TextArea rows={2} maxLength={500} />
              </Form.Item>
              <Button type="primary" block loading={submitting} onClick={submit}>
                Đặt lịch thăm
              </Button>
            </Form>
          </Card>
        </Col>
        <Col xs={24} lg={14}>
          <Card>
            <Tabs
              items={[
                {
                  key: 'upcoming',
                  label: `Sắp tới (${upcoming.length})`,
                  children: (
                    <Table
                      rowKey="id"
                      size="small"
                      dataSource={upcoming}
                      pagination={false}
                      columns={[
                        { title: 'Ngày', dataIndex: 'visitDate', render: dateOnly },
                        { title: 'Khung giờ', render: (_, b) => `${hhmm(b.slotStart)}–${hhmm(b.slotEnd)}` },
                        { title: 'Người thân', dataIndex: 'residentName' },
                        { title: 'Khách', dataIndex: 'guestCount' },
                        { title: 'Trạng thái', dataIndex: 'status', render: (v: string) => <StatusTag kind="visit" value={v} /> },
                        {
                          title: '',
                          key: 'a',
                          render: (_, b) =>
                            (b.status === 'Requested' || b.status === 'Approved') && (
                              <Popconfirm
                                title="Hủy lịch thăm này?"
                                onConfirm={async () => {
                                  try {
                                    await visitsApi.cancel(b.id);
                                    message.success('Đã hủy lịch thăm.');
                                    loadBookings();
                                    loadSlots(selectedDate);
                                  } catch (e) {
                                    message.error(extractErrorMessage(e));
                                  }
                                }}
                              >
                                <Button size="small" danger>Hủy</Button>
                              </Popconfirm>
                            ),
                        },
                      ]}
                    />
                  ),
                },
                {
                  key: 'history',
                  label: `Đã qua (${history.length})`,
                  children: (
                    <Table
                      rowKey="id"
                      size="small"
                      dataSource={history}
                      pagination={{ pageSize: 8 }}
                      columns={[
                        { title: 'Ngày', dataIndex: 'visitDate', render: dateOnly },
                        { title: 'Khung giờ', render: (_, b) => `${hhmm(b.slotStart)}–${hhmm(b.slotEnd)}` },
                        { title: 'Người thân', dataIndex: 'residentName' },
                        { title: 'Trạng thái', dataIndex: 'status', render: (v: string) => <StatusTag kind="visit" value={v} /> },
                        { title: 'Ghi chú duyệt', dataIndex: 'reviewNote' },
                      ]}
                    />
                  ),
                },
              ]}
            />
          </Card>
        </Col>
      </Row>
    </>
  );
}
