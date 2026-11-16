import { useCallback, useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import {
  App, Button, DatePicker, Form, Input, Modal, Popconfirm, Segmented, Select, Space, Table, Tabs, Tag, Typography,
} from 'antd';
import { LeftOutlined, ReloadOutlined, RightOutlined, UserAddOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import { admissionsApi, type TourAction } from '../../api/admissions';
import { extractErrorMessage } from '../../api/client';
import { containsAny, odataString } from '../../api/odata';
import { FRONT_DESK } from '../../auth/roles';
import { AccountSelect, PageTitle, StatusTag } from '../../components/Common';
import { dateOnly, dateTime, dayjs, todayVn } from '../../utils/format';
import { statusMeta, statusOptions } from '../../utils/status';
import type { Inquiry, InquiryStatus, TourBooking, UpdateInquiryForm } from '../../types/operations';
import { ResidentFormModal } from '../Staff/resident/ResidentFormModal';

const PAGE_SIZE = 10;

/** Bước tiếp theo hợp lệ (khớp AdmissionService.CanTransition). Admitted chỉ qua nút "Nhập viện". */
const NEXT_STATUS: Record<InquiryStatus, InquiryStatus[]> = {
  New: ['Contacted', 'Visited', 'Lost'],
  Contacted: ['Visited', 'Lost'],
  Visited: ['Lost'],
  Admitted: [],
  Lost: [],
};

/** UC-21, UC-22: lễ tân quản lý yêu cầu tư vấn (phễu khách) và lịch tham quan. */
export default function Admissions() {
  const [params, setParams] = useSearchParams();
  const tab = params.get('tab') === 'tours' ? 'tours' : 'inquiries';
  return (
    <>
      <PageTitle title="Tiếp nhận & tham quan" />
      <Tabs
        activeKey={tab}
        onChange={(key) => setParams(key === 'tours' ? { tab: 'tours' } : {})}
        items={[
          { key: 'inquiries', label: 'Yêu cầu tư vấn', children: <InquiriesTab /> },
          { key: 'tours', label: 'Lịch tham quan', children: <ToursTab /> },
        ]}
      />
    </>
  );
}

function InquiriesTab() {
  const { message } = App.useApp();
  const [rows, setRows] = useState<Inquiry[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [keyword, setKeyword] = useState('');
  const [status, setStatus] = useState<InquiryStatus | ''>('');
  const [editing, setEditing] = useState<Inquiry>();
  const [admitting, setAdmitting] = useState<Inquiry>();

  const load = useCallback(async () => {
    try {
      const result = await admissionsApi.inquiries({
        filter: [containsAny(['fullName', 'phone', 'elderName'], keyword) ?? '', status ? `status eq ${odataString(status)}` : ''],
        orderBy: 'createdAt desc', top: PAGE_SIZE, skip: (page - 1) * PAGE_SIZE,
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

  const admit = async (inquiry: Inquiry, residentId: number, residentCode: string) => {
    try {
      await admissionsApi.admit(inquiry.id, residentId);
      message.success(`Đã nhập viện: tạo cư dân ${residentCode} từ yêu cầu của ${inquiry.fullName}.`);
    } catch (e) {
      message.error(`Đã tạo cư dân ${residentCode} nhưng chưa gắn được vào yêu cầu: ${extractErrorMessage(e)}`);
    } finally {
      setAdmitting(undefined);
      void load();
    }
  };

  return (
    <>
      <Space style={{ marginBottom: 16 }} wrap>
        <Segmented<InquiryStatus | ''>
          value={status}
          onChange={(v) => { setPage(1); setStatus(v); }}
          options={[{ value: '', label: 'Tất cả' }, ...statusOptions('inquiry').map((o) => ({ ...o, value: o.value as InquiryStatus }))]}
        />
        <Input.Search placeholder="Tên, SĐT, người cao tuổi" allowClear onSearch={(v) => { setPage(1); setKeyword(v); }} style={{ width: 260 }} />
        <Button icon={<ReloadOutlined />} onClick={() => void load()}>Tải lại</Button>
      </Space>
      <Table
        rowKey="id"
        dataSource={rows}
        scroll={{ x: 1100 }}
        pagination={{ current: page, pageSize: PAGE_SIZE, total, onChange: setPage, showTotal: (t) => `${t} yêu cầu` }}
        columns={[
          { title: 'Ngày gửi', dataIndex: 'createdAt', width: 130, render: dateTime },
          {
            title: 'Người liên hệ', render: (_, i) => (
              <>
                <Typography.Text strong>{i.fullName}</Typography.Text>
                <div>{i.phone}</div>
                {i.email && <Typography.Text type="secondary">{i.email}</Typography.Text>}
              </>
            ),
          },
          { title: 'Người cao tuổi', render: (_, i) => (i.elderName ? `${i.elderName}${i.elderAge ? `, ${i.elderAge} tuổi` : ''}` : '—') },
          {
            title: 'Nhu cầu', render: (_, i) => (
              <>
                {i.interestedPackageName && <Tag color="green">{i.interestedPackageName}</Tag>}
                <Typography.Paragraph ellipsis={{ rows: 2, tooltip: i.careNeeds }} style={{ marginBottom: 0 }}>{i.careNeeds}</Typography.Paragraph>
              </>
            ),
          },
          { title: 'Nguồn', dataIndex: 'source', width: 120, render: (v: string) => <StatusTag kind="inquirySource" value={v} /> },
          {
            title: 'Tham quan', width: 150, render: (_, i) => (i.lastTourDate
              ? <>{dateOnly(i.lastTourDate)} · {i.lastTourSlot === 'Morning' ? 'Sáng' : 'Chiều'}<div>{i.lastTourStatus && <StatusTag kind="tour" value={i.lastTourStatus} />}</div></>
              : '—'),
          },
          { title: 'Phụ trách', dataIndex: 'assignedToName', render: (v?: string) => v ?? '—' },
          {
            title: 'Trạng thái', width: 140, render: (_, i) => (
              <>
                <StatusTag kind="inquiry" value={i.status} />
                {i.residentId && <div><Link to={`/staff/residents/${i.residentId}`}>{i.residentCode}</Link></div>}
              </>
            ),
          },
          {
            title: '', width: 190, fixed: 'right', render: (_, i) => (
              <Space>
                <Button size="small" onClick={() => setEditing(i)}>Cập nhật</Button>
                {i.status === 'Visited' && <Button size="small" type="primary" icon={<UserAddOutlined />} onClick={() => setAdmitting(i)}>Nhập viện</Button>}
              </Space>
            ),
          },
        ]}
      />
      {editing && <InquiryEditModal inquiry={editing} onClose={() => setEditing(undefined)} onSaved={() => { setEditing(undefined); void load(); }} />}
      <ResidentFormModal
        open={!!admitting}
        defaults={{ fullName: admitting?.elderName, notes: admitting?.careNeeds }}
        onClose={() => setAdmitting(undefined)}
        onSaved={(resident) => { if (admitting) void admit(admitting, resident.id, resident.code); }}
      />
    </>
  );
}

function InquiryEditModal({ inquiry, onClose, onSaved }: { inquiry: Inquiry; onClose: () => void; onSaved: () => void }) {
  const { message } = App.useApp();
  const [form] = Form.useForm<UpdateInquiryForm>();
  const [saving, setSaving] = useState(false);
  const allowed: InquiryStatus[] = [inquiry.status, ...NEXT_STATUS[inquiry.status]];

  const save = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      await admissionsApi.updateInquiry(inquiry.id, { ...values, assignedToId: values.assignedToId ?? null });
      message.success('Đã cập nhật yêu cầu.');
      onSaved();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal open title={`Yêu cầu của ${inquiry.fullName} · ${inquiry.phone}`} onOk={save} onCancel={onClose} okText="Lưu" confirmLoading={saving}>
      {inquiry.careNeeds && <Typography.Paragraph type="secondary">{inquiry.careNeeds}</Typography.Paragraph>}
      <Form form={form} layout="vertical"
        initialValues={{ status: inquiry.status, assignedToId: inquiry.assignedToId ?? undefined, notes: inquiry.notes ?? '' }}>
        <Form.Item name="status" label="Trạng thái" rules={[{ required: true }]}
          extra={inquiry.status === 'Visited' ? 'Dùng nút "Nhập viện" để tạo hồ sơ cư dân từ yêu cầu này.' : undefined}>
          <Select options={allowed.map((s) => ({ value: s, label: statusMeta('inquiry', s).label }))} disabled={allowed.length === 1} />
        </Form.Item>
        <Form.Item name="assignedToId" label="Người phụ trách"><AccountSelect roles={FRONT_DESK} /></Form.Item>
        <Form.Item name="notes" label="Ghi chú" rules={[{ max: 2000 }]}><Input.TextArea rows={4} maxLength={2000} /></Form.Item>
      </Form>
    </Modal>
  );
}

function ToursTab() {
  const { message } = App.useApp();
  const [week, setWeek] = useState<Dayjs>(() => dayjs(todayVn()).startOf('week'));
  const [rows, setRows] = useState<TourBooking[]>([]);
  const [loading, setLoading] = useState(false);
  const today = todayVn();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await admissionsApi.tours(week.format('YYYY-MM-DD'), week.add(6, 'day').format('YYYY-MM-DD')));
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [week, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const act = async (tour: TourBooking, action: TourAction, done: string) => {
    try {
      await admissionsApi.tourAction(tour.id, action);
      message.success(done);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  const guests = rows.filter((t) => t.status === 'Requested' || t.status === 'Confirmed').reduce((sum, t) => sum + t.guestCount, 0);

  return (
    <>
      <Space style={{ marginBottom: 16 }} wrap>
        <Button icon={<LeftOutlined />} onClick={() => setWeek(week.subtract(1, 'week'))} aria-label="Tuần trước" />
        <DatePicker picker="week" value={week} allowClear={false} onChange={(d) => d && setWeek(d.startOf('week'))}
          format={(d) => `${d.startOf('week').format('DD/MM')} – ${d.startOf('week').add(6, 'day').format('DD/MM/YYYY')}`} />
        <Button icon={<RightOutlined />} onClick={() => setWeek(week.add(1, 'week'))} aria-label="Tuần sau" />
        <Button onClick={() => setWeek(dayjs(todayVn()).startOf('week'))}>Tuần này</Button>
        <Typography.Text type="secondary">{rows.length} lịch · {guests} khách đang giữ chỗ</Typography.Text>
      </Space>
      <Table
        rowKey="id"
        dataSource={rows}
        loading={loading}
        pagination={false}
        scroll={{ x: 1000 }}
        columns={[
          { title: 'Ngày', dataIndex: 'visitDate', width: 150, render: (v: string) => <>{dateOnly(v)}<div><Typography.Text type="secondary">{dayjs(v).format('dddd')}</Typography.Text></div></> },
          { title: 'Buổi', dataIndex: 'timeSlot', width: 170, render: (v: string) => <StatusTag kind="tourSlot" value={v} /> },
          {
            title: 'Khách', render: (_, t) => (
              <>
                <Typography.Text strong>{t.fullName}</Typography.Text>
                <div>{t.phone}{t.email ? ` · ${t.email}` : ''}</div>
              </>
            ),
          },
          { title: 'Số khách', dataIndex: 'guestCount', width: 90, align: 'center' },
          { title: 'Ghi chú', dataIndex: 'note', render: (v?: string) => v ?? '' },
          {
            title: 'Trạng thái', width: 150, render: (_, t) => (
              <>
                <StatusTag kind="tour" value={t.status} />
                {t.confirmedByName && <div><Typography.Text type="secondary">{t.confirmedByName}</Typography.Text></div>}
              </>
            ),
          },
          {
            title: '', width: 260, fixed: 'right', render: (_, t) => {
              const open = t.status === 'Requested' || t.status === 'Confirmed';
              const reached = t.visitDate <= today;
              return (
                <Space wrap>
                  {t.status === 'Requested' && t.visitDate >= today && (
                    <Button size="small" type="primary" onClick={() => void act(t, 'confirm', 'Đã xác nhận lịch (gửi email nếu khách có email).')}>Xác nhận</Button>
                  )}
                  {open && reached && <Button size="small" onClick={() => void act(t, 'done', 'Đã ghi nhận khách đến tham quan.')}>Đã đến</Button>}
                  {open && reached && <Button size="small" danger onClick={() => void act(t, 'no-show', 'Đã ghi nhận khách không đến.')}>Không đến</Button>}
                  {open && (
                    <Popconfirm title="Hủy lịch tham quan này?" okText="Hủy lịch" cancelText="Không" onConfirm={() => act(t, 'cancel', 'Đã hủy lịch.')}>
                      <Button size="small" type="link" danger>Hủy</Button>
                    </Popconfirm>
                  )}
                </Space>
              );
            },
          },
        ]}
      />
    </>
  );
}
