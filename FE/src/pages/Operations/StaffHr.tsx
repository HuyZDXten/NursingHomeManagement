import { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  Alert, App, AutoComplete, Badge, Button, DatePicker, Descriptions, Drawer, Form, Input, Modal, Popconfirm, Space, Table, Tabs, Tag, Typography,
} from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import { extractErrorMessage } from '../../api/client';
import { staffHrApi } from '../../api/staffHr';
import { PageTitle, StatusTag } from '../../components/Common';
import { useLookupNames } from '../../hooks/useLookups';
import { ROLE_LABEL } from '../../utils/status';
import { dateOnly, dayjs, timeOf, todayVn } from '../../utils/format';
import type { AttendanceRecord, StaffCertificate, StaffProfile, StaffProfileSummary } from '../../types/operations';

const iso = (value?: Dayjs | null) => (value ? value.format('YYYY-MM-DD') : null);

function useRun() {
  const { message } = App.useApp();
  return useCallback(async (action: () => Promise<unknown>, ok: string) => {
    try {
      await action();
      message.success(ok);
      return true;
    } catch (e) {
      message.error(extractErrorMessage(e));
      return false;
    }
  }, [message]);
}

// ======================= UC-43 hồ sơ & chứng chỉ =======================
function ProfileDrawer({ accountId, onClose, onChanged }: { accountId: number | null; onClose: () => void; onChanged: () => void }) {
  const run = useRun();
  const positions = useLookupNames('Position');
  const departments = useLookupNames('Department');
  const [profile, setProfile] = useState<StaffProfile>();
  const [profileForm] = Form.useForm();
  const [certForm] = Form.useForm();
  const [editingCert, setEditingCert] = useState<StaffCertificate | 'new' | null>(null);

  const load = useCallback(async (id: number) => {
    const p = await staffHrApi.profile(id);
    setProfile(p);
    profileForm.setFieldsValue({ ...p, hireDate: p.hireDate ? dayjs(p.hireDate) : null });
  }, [profileForm]);

  useEffect(() => {
    setProfile(undefined);
    if (accountId) void load(accountId).catch(() => undefined);
  }, [accountId, load]);

  const saveProfile = async () => {
    const v = await profileForm.validateFields();
    if (await run(() => staffHrApi.saveProfile(accountId!, { ...v, hireDate: iso(v.hireDate) }), 'Đã lưu hồ sơ.')) {
      await load(accountId!);
      onChanged();
    }
  };

  const saveCert = async () => {
    const v = await certForm.validateFields();
    const body = { name: v.name, number: v.number, issuedBy: v.issuedBy, issuedOn: iso(v.issuedOn), expiresOn: iso(v.expiresOn) };
    const ok = await run(() => (editingCert === 'new' ? staffHrApi.addCertificate(accountId!, body) : staffHrApi.updateCertificate((editingCert as StaffCertificate).id, body)), 'Đã lưu chứng chỉ.');
    if (ok) {
      setEditingCert(null);
      await load(accountId!);
      onChanged();
    }
  };

  return (
    <Drawer open={accountId !== null} onClose={onClose} size={720} title={profile ? `${profile.fullName} — ${ROLE_LABEL[profile.role as keyof typeof ROLE_LABEL] ?? profile.role}` : 'Hồ sơ nhân viên'} destroyOnHidden loading={!profile}>
      {profile && (
        <>
          <Descriptions size="small" column={2} style={{ marginBottom: 16 }}>
            <Descriptions.Item label="Email">{profile.email}</Descriptions.Item>
            <Descriptions.Item label="Điện thoại">{profile.phone ?? '—'}</Descriptions.Item>
          </Descriptions>
          <Form form={profileForm} layout="vertical">
            <Space wrap style={{ width: '100%' }} align="start">
              <Form.Item name="position" label="Chức danh" rules={[{ max: 100 }]}><AutoComplete style={{ width: 220 }} options={positions.map((value) => ({ value }))} /></Form.Item>
              <Form.Item name="department" label="Bộ phận" rules={[{ max: 100 }]}><AutoComplete style={{ width: 200 }} options={departments.map((value) => ({ value }))} /></Form.Item>
              <Form.Item name="hireDate" label="Ngày vào làm"><DatePicker format="DD/MM/YYYY" disabledDate={(d) => d.isAfter(dayjs(), 'day')} /></Form.Item>
            </Space>
            <Form.Item name="idNumber" label="CCCD / CMND (nhạy cảm, chỉ Admin và chính nhân viên xem)" rules={[{ pattern: /^(\d{9}|\d{12})$/, message: 'CCCD 12 chữ số (CMND cũ 9 chữ số)' }]}>
              <Input style={{ maxWidth: 260 }} />
            </Form.Item>
            <Form.Item name="address" label="Địa chỉ" rules={[{ max: 300 }]}><Input /></Form.Item>
            <Space wrap align="start">
              <Form.Item name="emergencyContactName" label="Liên hệ khẩn cấp" rules={[{ max: 150 }]}><Input style={{ width: 240 }} /></Form.Item>
              <Form.Item name="emergencyContactPhone" label="Điện thoại" rules={[{ max: 30 }]}><Input style={{ width: 180 }} /></Form.Item>
            </Space>
            <Form.Item name="notes" label="Ghi chú" rules={[{ max: 500 }]}><Input.TextArea rows={2} /></Form.Item>
            <Button type="primary" onClick={saveProfile}>Lưu hồ sơ</Button>
          </Form>

          <Typography.Title level={5} style={{ marginTop: 24 }}>Chứng chỉ</Typography.Title>
          <Button size="small" icon={<PlusOutlined />} style={{ marginBottom: 8 }} onClick={() => { certForm.resetFields(); setEditingCert('new'); }}>Thêm chứng chỉ</Button>
          <Table rowKey="id" size="small" dataSource={profile.certificates} pagination={false} locale={{ emptyText: 'Chưa có chứng chỉ' }} columns={[
            { title: 'Chứng chỉ', render: (_, c) => <>{c.name}{c.number && <><br /><small>{c.number}</small></>}</> },
            { title: 'Cấp bởi', dataIndex: 'issuedBy' },
            { title: 'Hết hạn', render: (_, c) => (c.expiresOn ? dateOnly(c.expiresOn) : 'Không thời hạn') },
            { title: 'Tình trạng', render: (_, c) => <><StatusTag kind="certState" value={c.state} />{c.state === 'ExpiringSoon' && <small> còn {c.daysLeft} ngày</small>}</> },
            {
              title: '', key: 'actions', render: (_, c) => (
                <Space size={4}>
                  <Button size="small" onClick={() => { certForm.setFieldsValue({ ...c, issuedOn: c.issuedOn ? dayjs(c.issuedOn) : null, expiresOn: c.expiresOn ? dayjs(c.expiresOn) : null }); setEditingCert(c); }}>Sửa</Button>
                  <Popconfirm title="Xóa chứng chỉ?" onConfirm={async () => { if (await run(() => staffHrApi.deleteCertificate(c.id), 'Đã xóa.')) { await load(accountId!); onChanged(); } }}>
                    <Button size="small" danger>Xóa</Button>
                  </Popconfirm>
                </Space>
              ),
            },
          ]} />
          <Modal open={editingCert !== null} title={editingCert === 'new' ? 'Thêm chứng chỉ' : 'Sửa chứng chỉ'} okText="Lưu" onOk={saveCert} onCancel={() => setEditingCert(null)} destroyOnHidden>
            <Form form={certForm} layout="vertical">
              <Form.Item name="name" label="Tên chứng chỉ" rules={[{ required: true, max: 200 }]}><Input /></Form.Item>
              <Space wrap>
                <Form.Item name="number" label="Số" rules={[{ max: 50 }]}><Input style={{ width: 160 }} /></Form.Item>
                <Form.Item name="issuedBy" label="Nơi cấp" rules={[{ max: 200 }]}><Input style={{ width: 240 }} /></Form.Item>
              </Space>
              <Space wrap>
                <Form.Item name="issuedOn" label="Ngày cấp"><DatePicker format="DD/MM/YYYY" disabledDate={(d) => d.isAfter(dayjs(), 'day')} /></Form.Item>
                <Form.Item name="expiresOn" label="Hết hạn (bỏ trống nếu không thời hạn)"><DatePicker format="DD/MM/YYYY" /></Form.Item>
              </Space>
              <Typography.Text type="secondary">Đổi ngày hết hạn thì cảnh báo được gửi lại theo mốc mới.</Typography.Text>
            </Form>
          </Modal>
        </>
      )}
    </Drawer>
  );
}

function ProfilesTab() {
  const [rows, setRows] = useState<StaffProfileSummary[]>([]);
  const [open, setOpen] = useState<number | null>(null);
  const load = useCallback(() => staffHrApi.profiles().then(setRows).catch(() => undefined), []);
  useEffect(() => {
    void load();
  }, [load]);

  return (
    <>
      <Table rowKey="accountId" size="small" dataSource={rows} pagination={false} scroll={{ x: 760 }} columns={[
        { title: 'Nhân viên', render: (_, r) => <>{r.fullName}{!r.isActive && <Tag style={{ marginLeft: 6 }}>Đã khóa</Tag>}<br /><small>{r.email}</small></> },
        { title: 'Vai trò', render: (_, r) => ROLE_LABEL[r.role as keyof typeof ROLE_LABEL] ?? r.role },
        { title: 'Chức danh', dataIndex: 'position' },
        { title: 'Vào làm', render: (_, r) => dateOnly(r.hireDate) },
        {
          title: 'Chứng chỉ', render: (_, r) => (
            <Space size={4} wrap>
              <span>{r.certificateCount}</span>
              {r.expiringCount > 0 && <Tag color="warning">{r.expiringCount} sắp hết hạn</Tag>}
              {r.expiredCount > 0 && <Tag color="error">{r.expiredCount} đã hết hạn</Tag>}
            </Space>
          ),
        },
        { title: '', key: 'open', render: (_, r) => <Button size="small" onClick={() => setOpen(r.accountId)}>Hồ sơ</Button> },
      ]} />
      <ProfileDrawer accountId={open} onClose={() => setOpen(null)} onChanged={() => void load()} />
    </>
  );
}

// ======================= UC-43 cảnh báo chứng chỉ =======================
function ExpiringTab({ onCount }: { onCount: (n: number) => void }) {
  const { message } = App.useApp();
  const [rows, setRows] = useState<StaffCertificate[]>([]);
  const load = useCallback(() => staffHrApi.expiring().then((r) => { setRows(r); onCount(r.length); }).catch(() => undefined), [onCount]);
  useEffect(() => {
    void load();
  }, [load]);

  const sendNow = async () => {
    try {
      const result = await staffHrApi.runAlerts();
      message.success(result.sent > 0 ? `Đã gửi ${result.sent} cảnh báo mới (xét ${result.checked} chứng chỉ).` : `Không có mốc mới cần báo (xét ${result.checked} chứng chỉ).`);
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <>
      <Space style={{ marginBottom: 12 }} wrap>
        <Button onClick={sendNow}>Gửi cảnh báo ngay</Button>
        <Typography.Text type="secondary">Hệ thống tự kiểm tra mỗi 6 giờ; mỗi mốc (còn ≤ 30 ngày, đã hết hạn) chỉ báo một lần cho nhân viên và Admin.</Typography.Text>
      </Space>
      <Table rowKey="id" size="small" dataSource={rows} pagination={false} locale={{ emptyText: 'Không có chứng chỉ nào sắp hết hạn.' }} columns={[
        { title: 'Nhân viên', dataIndex: 'accountName' },
        { title: 'Chứng chỉ', render: (_, c) => <>{c.name}{c.number && <small> ({c.number})</small>}</> },
        { title: 'Hết hạn', render: (_, c) => dateOnly(c.expiresOn) },
        { title: 'Tình trạng', render: (_, c) => <><StatusTag kind="certState" value={c.state} /><small> {c.state === 'Expired' ? `quá ${-(c.daysLeft ?? 0)} ngày` : `còn ${c.daysLeft} ngày`}</small></> },
      ]} />
      {rows.length > 0 && <Alert type="info" showIcon style={{ marginTop: 12 }} title="Gia hạn xong thì vào hồ sơ nhân viên, đổi ngày hết hạn của chứng chỉ." />}
    </>
  );
}

// ======================= UC-48 chấm công =======================
function AttendanceTab() {
  const run = useRun();
  const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [dayjs(todayVn()).subtract(13, 'day'), dayjs(todayVn())]);
  const [rows, setRows] = useState<AttendanceRecord[]>([]);
  const [editing, setEditing] = useState<AttendanceRecord | null>(null);
  const [form] = Form.useForm();
  const from = range[0].format('YYYY-MM-DD');
  const to = range[1].format('YYYY-MM-DD');
  const load = useCallback(() => staffHrApi.attendance(from, to).then(setRows).catch(() => undefined), [from, to]);
  useEffect(() => {
    void load();
  }, [load]);

  const open = (row: AttendanceRecord) => {
    form.setFieldsValue({ checkInAt: row.checkInAt ? dayjs(row.checkInAt) : dayjs(row.startsAt), checkOutAt: row.checkOutAt ? dayjs(row.checkOutAt) : dayjs(row.endsAt), reason: '' });
    setEditing(row);
  };
  const save = async () => {
    const v = await form.validateFields();
    const ok = await run(() => staffHrApi.editAttendance(editing!.shiftAssignmentId, {
      checkInAt: v.checkInAt.toISOString(), checkOutAt: v.checkOutAt ? v.checkOutAt.toISOString() : null, reason: v.reason,
    }), 'Đã sửa công.');
    if (ok) {
      setEditing(null);
      void load();
    }
  };

  return (
    <>
      <Space style={{ marginBottom: 12 }} wrap>
        <DatePicker.RangePicker value={range} format="DD/MM/YYYY" allowClear={false}
          onChange={(v) => { if (v?.[0] && v[1]) setRange([v[0], v[1]]); }} />
        <Typography.Text type="secondary">Vào ca từ 30 phút trước giờ bắt đầu; trễ quá 15 phút tính đi muộn. Sửa công phải ghi lý do.</Typography.Text>
      </Space>
      <Table rowKey="shiftAssignmentId" size="small" dataSource={rows} pagination={{ pageSize: 25 }} scroll={{ x: 800 }} columns={[
        { title: 'Ngày', render: (_, r) => dateOnly(r.workDate) },
        { title: 'Ca', dataIndex: 'shiftName' },
        { title: 'Nhân viên', dataIndex: 'accountName' },
        { title: 'Vào', render: (_, r) => (r.checkInAt ? timeOf(r.checkInAt) : '—') },
        { title: 'Ra', render: (_, r) => (r.checkOutAt ? timeOf(r.checkOutAt) : '—') },
        { title: 'Tình trạng', render: (_, r) => <><StatusTag kind="attendance" value={r.status} />{r.lateMinutes > 0 && <small> {r.lateMinutes} phút</small>}</> },
        { title: 'Đã sửa', render: (_, r) => r.editReason && <small>{r.editReason}</small> },
        { title: '', key: 'edit', render: (_, r) => r.status !== 'Upcoming' && <Button size="small" onClick={() => open(r)}>{r.status === 'Absent' ? 'Bổ sung công' : 'Sửa'}</Button> },
      ]} />
      <Modal open={!!editing} title={editing ? `Sửa công — ${editing.accountName}, ${editing.shiftName} ${dateOnly(editing.workDate)}` : ''} okText="Lưu" onOk={save} onCancel={() => setEditing(null)} destroyOnHidden>
        <Form form={form} layout="vertical">
          <Form.Item name="checkInAt" label="Giờ vào ca" rules={[{ required: true, message: 'Nhập giờ vào ca' }]}><DatePicker showTime format="DD/MM/YYYY HH:mm" /></Form.Item>
          <Form.Item name="checkOutAt" label="Giờ ra ca (bỏ trống nếu chưa ra)"><DatePicker showTime format="DD/MM/YYYY HH:mm" /></Form.Item>
          <Form.Item name="reason" label="Lý do (bắt buộc)" rules={[{ required: true, max: 300, message: 'Nhập lý do sửa công' }]}><Input.TextArea rows={2} /></Form.Item>
        </Form>
      </Modal>
    </>
  );
}

/** BL-V4: nhân sự (Admin): hồ sơ & chứng chỉ, cảnh báo chứng chỉ, chấm công. Hiệu suất nhân viên ở trang Báo cáo. */
export default function StaffHr() {
  const [params, setParams] = useSearchParams();
  const [expiring, setExpiring] = useState(0);
  const items = [
    { key: 'profiles', label: 'Nhân sự', children: <ProfilesTab /> },
    { key: 'expiring', label: <Badge count={expiring} size="small" offset={[8, 0]}>Chứng chỉ sắp hết hạn</Badge>, children: <ExpiringTab onCount={setExpiring} /> },
    { key: 'attendance', label: 'Chấm công', children: <AttendanceTab /> },
  ];
  // Tải số chứng chỉ cần chú ý cho huy hiệu ngay từ đầu (tab chưa mở).
  useEffect(() => {
    staffHrApi.expiring().then((r) => setExpiring(r.length)).catch(() => undefined);
  }, []);
  return (
    <>
      <PageTitle title="Nhân sự & chấm công" />
      <Tabs items={items} activeKey={params.get('tab') ?? 'profiles'} onChange={(tab) => setParams({ tab })} />
    </>
  );
}
