import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Alert, App, Badge, Button, Card, Checkbox, DatePicker, Empty, Form, Input, List, Modal, Popconfirm, Segmented, Select, Space,
  Table, Tag, Tooltip, Typography,
} from 'antd';
import { BulbOutlined, CheckOutlined, PlusOutlined, RobotOutlined, SwapOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import { extractErrorMessage } from '../../api/client';
import { handoverApi, staffingApi } from '../../api/staffing';
import { useAuth } from '../../auth/AuthContext';
import { ADMIN_ONLY, hasRole } from '../../auth/roles';
import { FACILITY_TZ, dateOnly, dateTime, dayjs, hhmm, timeOf, todayVn } from '../../utils/format';
import { ROLE_LABEL, statusMeta, statusOptions } from '../../utils/status';
import type { Role } from '../../types';
import type {
  Handover, HandoverDraft, HandoverItem, LeaveRequest, LeaveShift, LeaveStatus, LeaveType, Shift, ShiftAssignment,
} from '../../types/operations';

const SHIFT_ROLES: Role[] = ['Doctor', 'Nurse', 'Caregiver'];

const shiftLabel = (shift?: LeaveShift | null) =>
  (shift ? `${shift.shiftName} ${dayjs(shift.workDate).format('dd DD/MM')}` : '(ca đã bị bỏ khỏi lịch)');

const assignmentLabel = (a: ShiftAssignment, withName = false) =>
  `${withName ? `${a.accountName} · ` : ''}${a.shiftName} ${dayjs(a.workDate).format('dd DD/MM')} (${timeOf(a.startsAt)}–${timeOf(a.endsAt)})`;

// ======================= UC-47 nghỉ phép / đổi ca =======================

export function LeaveTab() {
  const { message, modal } = App.useApp();
  const { user } = useAuth();
  const isAdmin = hasRole(user?.role, ADMIN_ONLY);
  const canRequest = hasRole(user?.role, SHIFT_ROLES);
  const [status, setStatus] = useState<LeaveStatus | undefined>(isAdmin ? 'Pending' : undefined);
  const [rows, setRows] = useState<LeaveRequest[]>([]);
  const [loading, setLoading] = useState(false);
  const [creating, setCreating] = useState(false);
  const [reviewing, setReviewing] = useState<{ request: LeaveRequest; approve: boolean }>();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await handoverApi.leaveRequests(status));
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [status, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const cancel = async (row: LeaveRequest) => {
    try {
      await handoverApi.cancel(row.id);
      message.success('Đã hủy đơn.');
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  const reviewed = (vacated?: LeaveShift[]) => {
    setReviewing(undefined);
    void load();
    if (vacated?.length) {
      modal.warning({
        title: 'Cần xếp người thay',
        content: (
          <>
            <p>Đã bỏ {vacated.length} ca khỏi lịch. Vào tab "Lịch ca tuần" để xếp người thay:</p>
            <ul>{vacated.map((s) => <li key={s.assignmentId}>{shiftLabel(s)}</li>)}</ul>
          </>
        ),
      });
    }
  };

  return (
    <>
      <Space style={{ marginBottom: 16 }} wrap>
        {canRequest && <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreating(true)}>Tạo đơn</Button>}
        <Select<LeaveStatus> allowClear placeholder="Mọi trạng thái" style={{ width: 180 }} value={status} onChange={setStatus}
          options={statusOptions('leave')} />
        <Typography.Text type="secondary">
          {isAdmin ? 'Duyệt đổi ca sẽ hoán đổi lịch của hai người; duyệt nghỉ sẽ bỏ các ca trong khoảng nghỉ.' : 'Đơn của bạn và đơn đồng nghiệp xin đổi ca với bạn.'}
        </Typography.Text>
      </Space>
      <Table<LeaveRequest>
        rowKey="id"
        dataSource={rows}
        loading={loading}
        pagination={{ pageSize: 10, hideOnSinglePage: true }}
        scroll={{ x: 1000 }}
        columns={[
          {
            title: 'Người xin', render: (_, r) => (
              <>{r.accountName} <Typography.Text type="secondary">({ROLE_LABEL[r.role as Role] ?? r.role})</Typography.Text></>
            ),
          },
          { title: 'Loại', dataIndex: 'type', width: 110, render: (v: LeaveType) => <Tag color={statusMeta('leaveType', v).color}>{statusMeta('leaveType', v).label}</Tag> },
          {
            title: 'Nội dung', render: (_, r) => (r.type === 'Leave'
              ? `Nghỉ ${dateOnly(r.fromDate)} – ${dateOnly(r.toDate)}`
              : <>{shiftLabel(r.myShift)} <SwapOutlined /> {shiftLabel(r.theirShift)} của {r.swapWithName}</>),
          },
          { title: 'Lý do', dataIndex: 'reason', ellipsis: true },
          {
            title: 'Trạng thái', dataIndex: 'status', width: 130, render: (v: LeaveStatus, r) => {
              const tag = <Tag color={statusMeta('leave', v).color}>{statusMeta('leave', v).label}</Tag>;
              const detail = [r.reviewedByName, r.reviewNote].filter(Boolean).join(': ');
              return detail ? <Tooltip title={detail}>{tag}</Tooltip> : tag;
            },
          },
          { title: 'Tạo lúc', dataIndex: 'createdAt', width: 150, render: dateTime },
          {
            title: '', width: 170, render: (_, r) => {
              if (r.status !== 'Pending') return null;
              if (isAdmin) {
                return (
                  <Space>
                    <Button size="small" type="primary" onClick={() => setReviewing({ request: r, approve: true })}>Duyệt</Button>
                    <Button size="small" danger onClick={() => setReviewing({ request: r, approve: false })}>Từ chối</Button>
                  </Space>
                );
              }
              return r.accountId === user?.accountId ? (
                <Popconfirm title="Hủy đơn này?" okText="Hủy đơn" cancelText="Không" onConfirm={() => cancel(r)}>
                  <Button size="small">Hủy đơn</Button>
                </Popconfirm>
              ) : null;
            },
          },
        ]}
      />
      {creating && <LeaveRequestModal onClose={() => setCreating(false)} onSaved={() => { setCreating(false); void load(); }} />}
      {reviewing && <ReviewModal {...reviewing} onClose={() => setReviewing(undefined)} onDone={reviewed} />}
    </>
  );
}

function ReviewModal({ request, approve, onClose, onDone }: {
  request: LeaveRequest;
  approve: boolean;
  onClose: () => void;
  onDone: (vacated?: LeaveShift[]) => void;
}) {
  const { message } = App.useApp();
  const [note, setNote] = useState('');
  const [saving, setSaving] = useState(false);

  const submit = async () => {
    setSaving(true);
    try {
      if (approve) {
        const result = await handoverApi.approve(request.id, note || undefined);
        message.success('Đã duyệt đơn.');
        onDone(result.vacatedShifts);
      } else {
        await handoverApi.reject(request.id, note || undefined);
        message.success('Đã từ chối đơn.');
        onDone();
      }
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal open title={`${approve ? 'Duyệt' : 'Từ chối'} đơn của ${request.accountName}`} onOk={submit} onCancel={onClose}
      okText={approve ? 'Duyệt' : 'Từ chối'} okButtonProps={{ danger: !approve }} confirmLoading={saving}>
      <p>
        {request.type === 'Leave'
          ? `Nghỉ ${dateOnly(request.fromDate)} – ${dateOnly(request.toDate)}. Các ca chưa bắt đầu trong khoảng này sẽ bị bỏ khỏi lịch.`
          : `Đổi ${shiftLabel(request.myShift)} lấy ${shiftLabel(request.theirShift)} của ${request.swapWithName}.`}
      </p>
      <p><Typography.Text type="secondary">Lý do: {request.reason}</Typography.Text></p>
      <Input.TextArea rows={3} maxLength={500} placeholder={approve ? 'Ghi chú (không bắt buộc)' : 'Lý do từ chối (người xin sẽ thấy)'}
        value={note} onChange={(e) => setNote(e.target.value)} />
    </Modal>
  );
}

function LeaveRequestModal({ onClose, onSaved }: { onClose: () => void; onSaved: () => void }) {
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const [type, setType] = useState<LeaveType>('Leave');
  const [mine, setMine] = useState<ShiftAssignment[]>([]);
  const [candidates, setCandidates] = useState<ShiftAssignment[]>([]);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (type !== 'Swap') return;
    staffingApi.mine(28)
      .then((items) => setMine(items.filter((a) => dayjs(a.startsAt).isAfter(dayjs()))))
      .catch((e) => message.error(extractErrorMessage(e)));
  }, [type, message]);

  const pickMine = async (id?: number) => {
    form.setFieldValue('swapShiftAssignmentId', undefined);
    setCandidates([]);
    if (!id) return;
    try {
      setCandidates(await handoverApi.swapCandidates(id));
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  const save = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      const range = values.range as [Dayjs, Dayjs] | undefined;
      await handoverApi.createLeave(type === 'Leave'
        ? { type, fromDate: range?.[0].format('YYYY-MM-DD'), toDate: range?.[1].format('YYYY-MM-DD'), reason: values.reason }
        : { type, shiftAssignmentId: values.shiftAssignmentId, swapShiftAssignmentId: values.swapShiftAssignmentId, reason: values.reason });
      message.success('Đã gửi đơn, chờ Admin duyệt.');
      onSaved();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal open title="Tạo đơn nghỉ phép / đổi ca" onOk={save} onCancel={onClose} okText="Gửi đơn" confirmLoading={saving}>
      <Segmented<LeaveType> block value={type} onChange={setType} style={{ marginBottom: 16 }}
        options={[{ value: 'Leave', label: 'Nghỉ phép' }, { value: 'Swap', label: 'Đổi ca' }]} />
      <Form form={form} layout="vertical">
        {type === 'Leave' ? (
          <Form.Item name="range" label="Nghỉ từ ngày – đến ngày" rules={[{ required: true, message: 'Chọn khoảng ngày nghỉ' }]}
            extra="Tối đa 31 ngày. Khi được duyệt, các ca của bạn trong khoảng này sẽ được bỏ khỏi lịch.">
            <DatePicker.RangePicker format="DD/MM/YYYY" style={{ width: '100%' }} disabledDate={(d) => d.isBefore(dayjs(todayVn()), 'day')} />
          </Form.Item>
        ) : (
          <>
            <Form.Item name="shiftAssignmentId" label="Ca của bạn" rules={[{ required: true, message: 'Chọn ca muốn đổi' }]}>
              <Select placeholder={mine.length ? 'Chọn ca (4 tuần tới)' : 'Bạn chưa có ca nào sắp tới'} onChange={pickMine}
                options={mine.map((a) => ({ value: a.id, label: assignmentLabel(a) }))} />
            </Form.Item>
            <Form.Item name="swapShiftAssignmentId" label="Ca của đồng nghiệp (cùng vai trò)" rules={[{ required: true, message: 'Chọn ca muốn nhận' }]}
              extra="Chỉ hiện ca đổi được: chưa bắt đầu, không làm ai bị trùng ca sau khi đổi.">
              <Select showSearch optionFilterProp="label" placeholder="Chọn ca của bạn trước" disabled={!candidates.length}
                options={candidates.map((a) => ({ value: a.id, label: assignmentLabel(a, true) }))} />
            </Form.Item>
          </>
        )}
        <Form.Item name="reason" label="Lý do" rules={[{ required: true, min: 3, message: 'Nhập lý do (ít nhất 3 ký tự)' }]}>
          <Input.TextArea rows={3} maxLength={500} showCount />
        </Form.Item>
      </Form>
    </Modal>
  );
}

// ======================= UC-49 bàn giao ca =======================

/** Ca đang diễn ra theo giờ cơ sở: ca đêm trước 06:00 thuộc ngày hôm trước. */
function currentShift(shifts: Shift[]): { shiftId?: number; date: string } {
  const now = dayjs().tz(FACILITY_TZ).format('HH:mm');
  for (const s of shifts) {
    const start = hhmm(s.startTime);
    const end = hhmm(s.endTime);
    if (!s.crossesMidnight && now >= start && now < end) return { shiftId: s.id, date: todayVn() };
    if (s.crossesMidnight && now >= start) return { shiftId: s.id, date: todayVn() };
    if (s.crossesMidnight && now < end) return { shiftId: s.id, date: dayjs(todayVn()).subtract(1, 'day').format('YYYY-MM-DD') };
  }
  return { shiftId: shifts[0]?.id, date: todayVn() };
}

/** Khóa của mục gợi ý = vị trí trong bản nháp (các mục không có Id). */
const itemKey = (_item: HandoverItem, index: number) => String(index);

const composeSummary = (items: HandoverItem[]) =>
  items.map((item) => `- ${item.residentName ? `${item.residentName}: ` : ''}${item.text}`).join('\n');

export function HandoverTab({ shifts, onInboxChange }: { shifts: Shift[]; onInboxChange: (count: number) => void }) {
  const { message } = App.useApp();
  const [inbox, setInbox] = useState<Handover[]>([]);
  const [date, setDate] = useState(() => todayVn());
  const [rows, setRows] = useState<Handover[]>([]);

  const loadInbox = useCallback(async () => {
    try {
      const items = await handoverApi.inbox();
      setInbox(items);
      onInboxChange(items.length);
    } catch {
      // badge không quan trọng bằng màn chính
    }
  }, [onInboxChange]);

  const loadList = useCallback(async () => {
    try {
      setRows(await handoverApi.list(date));
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  }, [date, message]);

  useEffect(() => {
    void loadInbox();
  }, [loadInbox]);

  useEffect(() => {
    void loadList();
  }, [loadList]);

  const acknowledge = async (handover: Handover) => {
    try {
      await handoverApi.acknowledge(handover.id);
      message.success('Đã xác nhận đọc bàn giao.');
      void loadInbox();
      void loadList();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      {inbox.length > 0 && (
        <Card size="small" title={<Badge status="warning" text={`Bàn giao chờ bạn xác nhận (${inbox.length})`} />}>
          <List dataSource={inbox} renderItem={(h) => <HandoverCard handover={h} onAcknowledge={acknowledge} />} />
        </Card>
      )}
      <HandoverWriter shifts={shifts} onSaved={(saved) => { setDate(saved.workDate); void loadList(); }} />
      <Card size="small" title="Bàn giao trong ngày"
        extra={<DatePicker value={dayjs(date)} allowClear={false} format="DD/MM/YYYY" onChange={(d) => d && setDate(d.format('YYYY-MM-DD'))} />}>
        {rows.length
          ? <List dataSource={rows} renderItem={(h) => <HandoverCard handover={h} onAcknowledge={acknowledge} />} />
          : <Empty description="Chưa có bàn giao" />}
      </Card>
    </Space>
  );
}

function HandoverCard({ handover: h, onAcknowledge }: { handover: Handover; onAcknowledge: (h: Handover) => void }) {
  return (
    <List.Item
      actions={h.acknowledgedAt
        ? [<Typography.Text key="ack" type="success"><CheckOutlined /> {h.acknowledgedByName} đã đọc {timeOf(h.acknowledgedAt)}</Typography.Text>]
        : [
          <Badge key="unread" status="warning" text="Chưa đọc" />,
          ...(h.canAcknowledge ? [<Button key="btn" size="small" type="primary" onClick={() => onAcknowledge(h)}>Đã đọc</Button>] : []),
        ]}
    >
      <List.Item.Meta
        title={(
          <Space wrap>
            <Tag color={statusMeta('shift', h.shiftCode).color}>
              {h.shiftName} {dateOnly(h.workDate)}
            </Tag>
            <span>{h.fromAccountName}</span>
            <Typography.Text type="secondary">({ROLE_LABEL[h.role as Role] ?? h.role}) · {dateTime(h.createdAt)}</Typography.Text>
            {h.aiGenerated && <Tag color="purple">AI soạn, người viết đã duyệt</Tag>}
          </Space>
        )}
        description={(
          <>
            <Typography.Paragraph style={{ whiteSpace: 'pre-wrap', marginBottom: 8 }}>{h.summary}</Typography.Paragraph>
            {h.highlights.map((item, i) => (
              <div key={itemKey(item, i)}>
                <Tag color={statusMeta('handoverKind', item.kind).color}>{statusMeta('handoverKind', item.kind).label}</Tag>
                {item.residentId ? <Link to={`/staff/residents/${item.residentId}`}>{item.residentName ?? 'Cư dân'}</Link> : item.residentName}
                {item.residentName || item.residentId ? ': ' : ''}{item.text}
              </div>
            ))}
          </>
        )}
      />
    </List.Item>
  );
}

function HandoverWriter({ shifts, onSaved }: { shifts: Shift[]; onSaved: (saved: Handover) => void }) {
  const { message } = App.useApp();
  const initial = useMemo(() => currentShift(shifts), [shifts]);
  const [date, setDate] = useState(initial.date);
  const [shiftId, setShiftId] = useState<number | undefined>(initial.shiftId);
  const [draft, setDraft] = useState<HandoverDraft>();
  const [checked, setChecked] = useState<string[]>([]);
  const [summary, setSummary] = useState('');
  const [edited, setEdited] = useState(false);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  // S4-V2 (AI-04): nháp do AI soạn — người viết đọc, sửa rồi mới bấm Lưu.
  const [aiLoading, setAiLoading] = useState(false);
  const [aiGenerated, setAiGenerated] = useState(false);
  const [aiNote, setAiNote] = useState<string>();

  useEffect(() => {
    setDate(initial.date);
    setShiftId(initial.shiftId);
  }, [initial]);

  const writeWithAi = async () => {
    if (!shiftId) return;
    setAiLoading(true);
    try {
      const [base, ai] = await Promise.all([handoverApi.draft(shiftId, date), handoverApi.aiDraft(shiftId, date)]);
      setDraft(base);
      setChecked(base.items.map(itemKey));
      setSummary(ai.summary);
      setEdited(true); // bỏ chọn mục không ghi đè văn bản AI
      setAiGenerated(ai.aiGenerated);
      setAiNote(ai.note ?? 'Nội dung do AI soạn — kiểm tra và sửa lại trước khi lưu.');
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setAiLoading(false);
    }
  };

  const selectedItems = (keys: string[]) => (draft?.items ?? []).filter((item, i) => keys.includes(itemKey(item, i)));

  const suggest = async () => {
    if (!shiftId) return;
    setLoading(true);
    try {
      const result = await handoverApi.draft(shiftId, date);
      const keys = result.items.map(itemKey);
      setDraft(result);
      setChecked(keys);
      if (!edited) setSummary(composeSummary(result.items));
      if (!result.items.length) message.info('Không có việc tồn, thuốc bỏ lỡ hay sinh hiệu bất thường trong ca.');
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  };

  const toggle = (keys: string[]) => {
    setChecked(keys);
    if (!edited && draft) setSummary(composeSummary(draft.items.filter((item, i) => keys.includes(itemKey(item, i)))));
  };

  const save = async () => {
    if (!shiftId) return;
    if (summary.trim().length < 5) {
      message.warning('Nhập nội dung bàn giao (ít nhất 5 ký tự).');
      return;
    }
    setSaving(true);
    try {
      const saved = await handoverApi.create({ shiftId, workDate: date, summary: summary.trim(), highlights: selectedItems(checked), aiGenerated });
      message.success('Đã lưu bàn giao. Người ca sau đã được báo.');
      setDraft(undefined);
      setChecked([]);
      setSummary('');
      setEdited(false);
      setAiGenerated(false);
      setAiNote(undefined);
      onSaved(saved);
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card size="small" title="Viết bàn giao ca">
      <Space wrap style={{ marginBottom: 12 }}>
        <DatePicker value={dayjs(date)} allowClear={false} format="DD/MM/YYYY"
          disabledDate={(d) => d.isAfter(dayjs(todayVn()), 'day')}
          onChange={(d) => { if (d) { setDate(d.format('YYYY-MM-DD')); setDraft(undefined); } }} />
        <Select style={{ width: 220 }} value={shiftId} onChange={(v) => { setShiftId(v); setDraft(undefined); }}
          options={shifts.map((s) => ({ value: s.id, label: `${s.name} (${hhmm(s.startTime)}–${hhmm(s.endTime)})` }))} />
        <Button icon={<BulbOutlined />} loading={loading} onClick={suggest}>Lấy gợi ý</Button>
        <Button icon={<RobotOutlined />} loading={aiLoading} disabled={!shiftId} onClick={writeWithAi}>Viết tóm tắt bằng AI</Button>
      </Space>
      {aiNote && <Alert type="warning" showIcon style={{ marginBottom: 12 }} message={aiNote} />}
      {draft && draft.items.length > 0 && (
        <>
          <Typography.Text type="secondary">
            Dữ liệu từ {timeOf(draft.startsAt)} đến {timeOf(draft.endsAt)}. Bỏ chọn mục không cần bàn giao:
          </Typography.Text>
          <Checkbox.Group style={{ display: 'flex', flexDirection: 'column', gap: 4, margin: '8px 0 12px' }} value={checked} onChange={(v) => toggle(v as string[])}>
            {draft.items.map((item, i) => (
              <Checkbox key={itemKey(item, i)} value={itemKey(item, i)}>
                <Tag color={statusMeta('handoverKind', item.kind).color}>{statusMeta('handoverKind', item.kind).label}</Tag>
                {item.residentName ? `${item.residentName}: ` : ''}{item.text}
              </Checkbox>
            ))}
          </Checkbox.Group>
        </>
      )}
      <Input.TextArea rows={5} maxLength={4000} showCount value={summary}
        placeholder="Tình hình trong ca, việc ca sau cần làm tiếp, cư dân cần chú ý…"
        onChange={(e) => { setSummary(e.target.value); setEdited(true); }} />
      <Alert type="info" showIcon style={{ margin: '12px 0' }}
        message="Bàn giao là ghi chép nội bộ giữa các ca. Số liệu sinh hiệu chỉ để nhắc theo dõi, không phải chẩn đoán." />
      <Button type="primary" loading={saving} disabled={!shiftId} onClick={save}>Lưu bàn giao</Button>
    </Card>
  );
}
