import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import {
  App, Badge, Button, DatePicker, Form, List, Modal, Popconfirm, Popover, Select, Space, Switch, Table, Tabs, Tag, Typography,
} from 'antd';
import { CopyOutlined, LeftOutlined, PlusOutlined, RightOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import { accountsApi } from '../../api/accounts';
import { extractErrorMessage } from '../../api/client';
import { handoverApi, staffingApi } from '../../api/staffing';
import { useAuth } from '../../auth/AuthContext';
import { ADMIN_ONLY, CARE_PLANNERS, hasRole } from '../../auth/roles';
import { AccountSelect, PageTitle, ResidentSelect } from '../../components/Common';
import { dateOnly, dayjs, hhmm, timeOf, todayVn } from '../../utils/format';
import { ROLE_LABEL, statusMeta } from '../../utils/status';
import type { Role } from '../../types';
import type { CareAssignment, Shift, ShiftAssignment } from '../../types/operations';
import { HandoverTab, LeaveTab } from './ShiftRequests';

const ROLE_ORDER = ['Doctor', 'Nurse', 'Caregiver'];
const TEAM_VIEWERS: Role[] = ['Admin', 'Nurse'];

interface Person {
  id: number;
  name: string;
  role: string;
}

const TAB_KEYS = ['week', 'mine', 'care', 'leave', 'handover'];

/**
 * UC-44 lịch ca tuần, UC-46 lịch ca của tôi, UC-45 phân công phụ trách cư dân (S3-V2);
 * UC-47 nghỉ phép/đổi ca, UC-49 bàn giao ca (S3-V4). Tab đang mở nằm trên URL (?tab=) để thông báo trỏ thẳng vào.
 */
export default function Shifts() {
  const { user } = useAuth();
  const [params, setParams] = useSearchParams();
  const [shifts, setShifts] = useState<Shift[]>([]);
  const [inboxCount, setInboxCount] = useState(0);
  useEffect(() => {
    staffingApi.shifts().then(setShifts).catch(() => undefined);
    handoverApi.inbox().then((items) => setInboxCount(items.length)).catch(() => undefined);
  }, []);
  const seesTeam = hasRole(user?.role, TEAM_VIEWERS);
  const requested = params.get('tab');
  const activeKey = requested && TAB_KEYS.includes(requested) ? requested : seesTeam ? 'week' : 'mine';

  return (
    <>
      <PageTitle title="Ca trực" />
      <Tabs
        activeKey={activeKey}
        onChange={(key) => setParams({ tab: key }, { replace: true })}
        destroyOnHidden
        items={[
          { key: 'week', label: 'Lịch ca tuần', children: <WeekTab shifts={shifts} /> },
          { key: 'mine', label: 'Lịch ca của tôi', children: <MineTab /> },
          { key: 'care', label: 'Phân công phụ trách', children: <CareTab shifts={shifts} /> },
          { key: 'leave', label: 'Nghỉ phép / đổi ca', children: <LeaveTab /> },
          {
            key: 'handover',
            label: <Badge count={inboxCount} size="small" offset={[8, -2]}>Bàn giao ca</Badge>,
            children: <HandoverTab shifts={shifts} onInboxChange={setInboxCount} />,
          },
        ]}
      />
    </>
  );
}

function ShiftTag({ assignment, withTime }: { assignment: ShiftAssignment; withTime?: boolean }) {
  return (
    <Tag color={statusMeta('shift', assignment.shiftCode).color} style={{ marginInlineEnd: 4 }}>
      {assignment.shiftName}{withTime ? ` ${timeOf(assignment.startsAt)}–${timeOf(assignment.endsAt)}` : ''}
    </Tag>
  );
}

function WeekTab({ shifts }: { shifts: Shift[] }) {
  const { message } = App.useApp();
  const { user } = useAuth();
  const canEdit = hasRole(user?.role, ADMIN_ONLY);
  const seesTeam = hasRole(user?.role, TEAM_VIEWERS);
  const [week, setWeek] = useState<Dayjs>(() => dayjs(todayVn()).startOf('week'));
  const [staff, setStaff] = useState<Person[]>([]);
  const [rows, setRows] = useState<ShiftAssignment[]>([]);
  const [loading, setLoading] = useState(false);
  const today = todayVn();
  const days = useMemo(() => Array.from({ length: 7 }, (_, i) => week.add(i, 'day')), [week]);

  useEffect(() => {
    if (!seesTeam) return;
    accountsApi.lookup(['Doctor', 'Nurse', 'Caregiver'])
      .then((items) => setStaff(items
        .map((item) => ({ id: item.id, name: item.name, role: (item.extra ?? '').split(' · ')[0] }))
        .sort((a, b) => ROLE_ORDER.indexOf(a.role) - ROLE_ORDER.indexOf(b.role) || a.name.localeCompare(b.name, 'vi'))))
      .catch(() => undefined);
  }, [seesTeam]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await staffingApi.assignments(week.format('YYYY-MM-DD'), week.add(6, 'day').format('YYYY-MM-DD')));
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [week, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const people: Person[] = seesTeam ? staff : user ? [{ id: user.accountId, name: user.name, role: user.role }] : [];

  const toggle = async (person: Person, date: string, shift: Shift, existing?: ShiftAssignment) => {
    try {
      if (existing) {
        await staffingApi.unassign(existing.id);
        message.success(`Đã bỏ ${shift.name.toLowerCase()} của ${person.name}.`);
      } else {
        const created = await staffingApi.assign({ accountId: person.id, shiftId: shift.id, workDate: date });
        if (created.warning) message.warning(created.warning, 6);
        else message.success(`Đã xếp ${shift.name.toLowerCase()} cho ${person.name}.`);
      }
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  const copyPrevious = async () => {
    try {
      const result = await staffingApi.copyWeek(week.subtract(1, 'week').format('YYYY-MM-DD'), week.format('YYYY-MM-DD'));
      message.success(`Đã chép ${result.copied} ca từ tuần trước (bỏ qua ${result.skipped} ô đã có hoặc đã qua).`);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  const renderCell = (person: Person, day: Dayjs) => {
    const date = day.format('YYYY-MM-DD');
    const items = rows.filter((a) => a.accountId === person.id && a.workDate === date);
    const tags = items.length
      ? <div>{items.map((a) => <ShiftTag key={a.id} assignment={a} />)}</div>
      : <Typography.Text type="secondary">{canEdit && date >= today ? <PlusOutlined /> : '—'}</Typography.Text>;
    if (!canEdit || date < today) return tags;
    return (
      <Popover
        trigger="click"
        title={`${person.name} · ${day.format('dddd DD/MM')}`}
        content={(
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {shifts.map((shift) => {
              const existing = items.find((a) => a.shiftId === shift.id);
              return (
                <Button key={shift.id} size="small" block danger={!!existing} type={existing ? 'default' : 'dashed'}
                  onClick={() => void toggle(person, date, shift, existing)}>
                  {existing ? 'Bỏ' : 'Xếp'} {shift.name.toLowerCase()} ({hhmm(shift.startTime)}–{hhmm(shift.endTime)})
                </Button>
              );
            })}
          </div>
        )}
      >
        <div style={{ minHeight: 32, cursor: 'pointer' }}>{tags}</div>
      </Popover>
    );
  };

  return (
    <>
      <Space style={{ marginBottom: 16 }} wrap>
        <Button icon={<LeftOutlined />} onClick={() => setWeek(week.subtract(1, 'week'))} aria-label="Tuần trước" />
        <DatePicker picker="week" value={week} allowClear={false} onChange={(d) => d && setWeek(d.startOf('week'))}
          format={(d) => `${d.startOf('week').format('DD/MM')} – ${d.startOf('week').add(6, 'day').format('DD/MM/YYYY')}`} />
        <Button icon={<RightOutlined />} onClick={() => setWeek(week.add(1, 'week'))} aria-label="Tuần sau" />
        <Button onClick={() => setWeek(dayjs(todayVn()).startOf('week'))}>Tuần này</Button>
        {canEdit && (
          <Popconfirm title="Chép lịch tuần trước sang tuần này?" description="Ô đã có ca và ngày đã qua được giữ nguyên." okText="Sao chép" cancelText="Không" onConfirm={copyPrevious}>
            <Button icon={<CopyOutlined />}>Sao chép tuần trước</Button>
          </Popconfirm>
        )}
        {canEdit && <Typography.Text type="secondary">Bấm vào ô để xếp hoặc bỏ ca.</Typography.Text>}
      </Space>
      <Table<Person>
        rowKey="id"
        dataSource={people}
        loading={loading}
        pagination={false}
        scroll={{ x: 1100 }}
        columns={[
          {
            title: 'Nhân viên', fixed: 'left', width: 200, render: (_, p) => (
              <>
                <Typography.Text strong>{p.name}</Typography.Text>
                <div><Typography.Text type="secondary">{ROLE_LABEL[p.role as Role] ?? p.role}</Typography.Text></div>
              </>
            ),
          },
          ...days.map((day) => ({
            key: day.format('YYYY-MM-DD'),
            width: 130,
            title: (
              <span style={{ color: day.format('YYYY-MM-DD') === today ? '#2f7d6d' : undefined }}>
                {day.format('dd')}<br />{day.format('DD/MM')}
              </span>
            ),
            render: (_: unknown, p: Person) => renderCell(p, day),
          })),
        ]}
      />
    </>
  );
}

function MineTab() {
  const { message } = App.useApp();
  const [rows, setRows] = useState<ShiftAssignment[]>([]);
  const days = useMemo(() => Array.from({ length: 14 }, (_, i) => dayjs(todayVn()).add(i, 'day')), []);

  useEffect(() => {
    staffingApi.mine(14).then(setRows).catch((e) => message.error(extractErrorMessage(e)));
  }, [message]);

  return (
    <List
      dataSource={days}
      renderItem={(day) => {
        const items = rows.filter((a) => a.workDate === day.format('YYYY-MM-DD'));
        return (
          <List.Item>
            <List.Item.Meta
              title={day.format('dddd, DD/MM/YYYY')}
              description={items.length
                ? items.map((a) => <span key={a.id}><ShiftTag assignment={a} withTime />{a.note ? ` ${a.note}` : ''}</span>)
                : 'Nghỉ'}
            />
          </List.Item>
        );
      }}
    />
  );
}

function CareTab({ shifts }: { shifts: Shift[] }) {
  const { message } = App.useApp();
  const { user } = useAuth();
  const canEdit = hasRole(user?.role, CARE_PLANNERS);
  const [activeOnly, setActiveOnly] = useState(true);
  const [rows, setRows] = useState<CareAssignment[]>([]);
  const [adding, setAdding] = useState(false);

  const load = useCallback(async () => {
    try {
      setRows(await staffingApi.careAssignments({ activeOnly }));
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  }, [activeOnly, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const end = async (row: CareAssignment) => {
    try {
      await staffingApi.endCareAssignment(row.id);
      message.success(`Đã kết thúc phân công ${row.accountName} – ${row.residentCode}.`);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <>
      <Space style={{ marginBottom: 16 }} wrap>
        {canEdit && <Button type="primary" icon={<PlusOutlined />} onClick={() => setAdding(true)}>Phân công phụ trách</Button>}
        <Space><Switch checked={!activeOnly} onChange={(v) => setActiveOnly(!v)} /> Hiện cả phân công đã kết thúc</Space>
      </Space>
      <Table
        rowKey="id"
        dataSource={rows}
        pagination={{ pageSize: 10, hideOnSinglePage: true }}
        scroll={{ x: 900 }}
        columns={[
          { title: 'Cư dân', render: (_, a) => <Link to={`/staff/residents/${a.residentId}`}>{a.residentCode} · {a.residentName}</Link> },
          { title: 'Người phụ trách', render: (_, a) => <>{a.accountName} <Typography.Text type="secondary">({ROLE_LABEL[a.role as Role] ?? a.role})</Typography.Text></> },
          { title: 'Ca', dataIndex: 'shiftName', render: (v?: string) => v ?? 'Mọi ca' },
          { title: 'Từ ngày', dataIndex: 'startDate', render: dateOnly },
          { title: 'Đến ngày', dataIndex: 'endDate', render: (v?: string) => (v ? dateOnly(v) : '—') },
          { title: 'Người phân công', dataIndex: 'assignedByName' },
          { title: 'Trạng thái', dataIndex: 'isActive', render: (v: boolean) => (v ? <Tag color="green">Đang hiệu lực</Tag> : <Tag>Đã kết thúc</Tag>) },
          {
            title: '', render: (_, a) => (canEdit && a.isActive ? (
              <Popconfirm title="Kết thúc phân công này từ hôm nay?" okText="Kết thúc" cancelText="Không" onConfirm={() => end(a)}>
                <Button size="small" danger>Kết thúc</Button>
              </Popconfirm>
            ) : null),
          },
        ]}
      />
      {adding && <CareAssignmentModal shifts={shifts} onClose={() => setAdding(false)} onSaved={() => { setAdding(false); void load(); }} />}
    </>
  );
}

function CareAssignmentModal({ shifts, onClose, onSaved }: { shifts: Shift[]; onClose: () => void; onSaved: () => void }) {
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);

  const save = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      await staffingApi.addCareAssignment({
        residentId: values.residentId,
        accountId: values.accountId,
        shiftId: values.shiftId ?? null,
        startDate: (values.startDate as Dayjs).format('YYYY-MM-DD'),
      });
      message.success('Đã phân công phụ trách.');
      onSaved();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal open title="Phân công phụ trách cư dân" onOk={save} onCancel={onClose} okText="Phân công" confirmLoading={saving}>
      <Form form={form} layout="vertical" initialValues={{ startDate: dayjs(todayVn()) }}>
        <Form.Item name="residentId" label="Cư dân" rules={[{ required: true, message: 'Chọn cư dân' }]}><ResidentSelect /></Form.Item>
        <Form.Item name="accountId" label="Người phụ trách chính" rules={[{ required: true, message: 'Chọn nhân viên' }]}>
          <AccountSelect roles={['Nurse', 'Caregiver']} />
        </Form.Item>
        <Form.Item name="shiftId" label="Ca" extra="Bỏ trống = phụ trách mọi ca. Mỗi cư dân chỉ một người phụ trách chính cho mỗi ca.">
          <Select allowClear placeholder="Mọi ca" options={shifts.map((s) => ({ value: s.id, label: `${s.name} (${hhmm(s.startTime)}–${hhmm(s.endTime)})` }))} />
        </Form.Item>
        <Form.Item name="startDate" label="Từ ngày" rules={[{ required: true }]}>
          <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} disabledDate={(d) => d.isBefore(dayjs(todayVn()), 'day')} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
