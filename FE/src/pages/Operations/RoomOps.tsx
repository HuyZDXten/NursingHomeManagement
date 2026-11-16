import { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  App, Button, Checkbox, DatePicker, Form, Input, InputNumber, Modal, Popconfirm, Segmented, Select, Space, Switch, Table, Tabs, Typography,
} from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { admissionsApi } from '../../api/admissions';
import { extractErrorMessage } from '../../api/client';
import { roomOpsApi } from '../../api/roomOps';
import { roomsApi } from '../../api/rooms';
import { useAuth } from '../../auth/AuthContext';
import { ADMIN_ONLY, FRONT_DESK, hasRole } from '../../auth/roles';
import { PageTitle, StatusTag } from '../../components/Common';
import { dateTime, dayjs, money } from '../../utils/format';
import type { Room } from '../../types';
import type { BedReservation, Inquiry, MaintenanceRequest, MaintenanceStatus, RoomType } from '../../types/operations';

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

// ======================= UC-42 báo hỏng & sửa chữa =======================
function MaintenanceTab({ rooms, isAdmin }: { rooms: Room[]; isAdmin: boolean }) {
  const run = useRun();
  const [filter, setFilter] = useState<MaintenanceStatus | 'All'>('Open');
  const [rows, setRows] = useState<MaintenanceRequest[]>([]);
  const [open, setOpen] = useState(false);
  const [acting, setActing] = useState<{ row: MaintenanceRequest; status: 'InProgress' | 'Done' | 'Cancelled' } | null>(null);
  const [form] = Form.useForm();
  const [actForm] = Form.useForm();
  const roomId = Form.useWatch('roomId', form);
  const bedId = Form.useWatch('bedId', form);

  const load = useCallback(() => roomOpsApi.maintenance(filter === 'All' ? undefined : filter).then(setRows).catch(() => undefined), [filter]);
  useEffect(() => {
    void load();
  }, [load]);

  const bedsOfRoom = rooms.find((r) => r.id === roomId)?.beds ?? [];
  const save = async () => {
    const v = await form.validateFields();
    if (await run(() => roomOpsApi.report({ ...v, blocksBed: !!v.blocksBed && !!v.bedId }), 'Đã gửi báo hỏng.')) {
      setOpen(false);
      void load();
    }
  };
  const act = async () => {
    if (!acting) return;
    const v = await actForm.validateFields();
    if (await run(() => roomOpsApi.updateStatus(acting.row.id, { status: acting.status, ...v }), 'Đã cập nhật phiếu.')) {
      setActing(null);
      void load();
    }
  };

  return (
    <>
      <Space style={{ marginBottom: 12 }} wrap>
        <Segmented value={filter} onChange={(v) => setFilter(v as MaintenanceStatus | 'All')} options={[
          { value: 'Open', label: 'Mới báo' }, { value: 'InProgress', label: 'Đang sửa' }, { value: 'Done', label: 'Đã xong' }, { value: 'All', label: 'Tất cả' },
        ]} />
        <Button type="primary" icon={<PlusOutlined />} onClick={() => { form.resetFields(); setOpen(true); }}>Báo hỏng</Button>
      </Space>
      <Table rowKey="id" size="small" dataSource={rows} pagination={{ pageSize: 20 }} columns={[
        { title: 'Phòng/giường', render: (_, r) => <>Phòng {r.roomNumber}{r.bedNumber && ` - Giường ${r.bedNumber}`}{r.blocksBed && <small> (khóa giường)</small>}</> },
        { title: 'Nội dung', render: (_, r) => <><b>{r.title}</b>{r.description && <><br /><small>{r.description}</small></>}</> },
        { title: 'Mức độ', render: (_, r) => <StatusTag kind="priority" value={r.priority} /> },
        { title: 'Trạng thái', render: (_, r) => <><StatusTag kind="maintenance" value={r.status} />{r.assignedTo && <small> {r.assignedTo}</small>}</> },
        { title: 'Người báo', render: (_, r) => <>{r.reportedByName}<br /><small>{dateTime(r.reportedAt)}</small></> },
        { title: 'Kết quả', render: (_, r) => r.resolvedAt && <><small>{dateTime(r.resolvedAt)}</small><br />{r.resolutionNote}</> },
        {
          title: '', key: 'actions', render: (_, r) => isAdmin && (r.status === 'Open' || r.status === 'InProgress') && (
            <Space size={4}>
              {r.status === 'Open' && <Button size="small" onClick={() => { actForm.resetFields(); setActing({ row: r, status: 'InProgress' }); }}>Nhận sửa</Button>}
              <Button size="small" type="primary" onClick={() => { actForm.resetFields(); setActing({ row: r, status: 'Done' }); }}>Xong</Button>
              <Button size="small" danger onClick={() => { actForm.resetFields(); setActing({ row: r, status: 'Cancelled' }); }}>Hủy</Button>
            </Space>
          ),
        },
      ]} />

      <Modal open={open} title="Báo hỏng" okText="Gửi" onOk={save} onCancel={() => setOpen(false)} destroyOnHidden>
        <Form form={form} layout="vertical" initialValues={{ priority: 'Normal', blocksBed: false }}>
          <Form.Item name="roomId" label="Phòng" rules={[{ required: true, message: 'Chọn phòng' }]}>
            <Select options={rooms.map((r) => ({ value: r.id, label: `Phòng ${r.number}` }))} onChange={() => form.setFieldValue('bedId', undefined)} />
          </Form.Item>
          <Form.Item name="bedId" label="Giường (nếu hỏng ở giường)">
            <Select allowClear options={bedsOfRoom.map((b) => ({ value: b.id, label: `Giường ${b.number}` }))} />
          </Form.Item>
          <Form.Item name="title" label="Hỏng gì" rules={[{ required: true, max: 200 }]}><Input placeholder="vd. Điều hòa chảy nước" /></Form.Item>
          <Form.Item name="description" label="Mô tả thêm" rules={[{ max: 1000 }]}><Input.TextArea rows={2} /></Form.Item>
          <Form.Item name="priority" label="Mức độ">
            <Select options={[{ value: 'Low', label: 'Thấp' }, { value: 'Normal', label: 'Bình thường' }, { value: 'High', label: 'Khẩn (báo ngay Admin)' }]} />
          </Form.Item>
          <Form.Item name="blocksBed" valuePropName="checked"><Checkbox disabled={!bedId}>Khóa giường này tới khi sửa xong</Checkbox></Form.Item>
        </Form>
      </Modal>

      <Modal open={!!acting} title={acting?.status === 'InProgress' ? 'Nhận sửa' : acting?.status === 'Done' ? 'Đã sửa xong' : 'Hủy phiếu'}
        okText="Lưu" onOk={act} onCancel={() => setActing(null)} destroyOnHidden>
        <Form form={actForm} layout="vertical">
          <Form.Item name="assignedTo" label="Người/đơn vị sửa" rules={[{ max: 150 }]}><Input /></Form.Item>
          {acting?.status !== 'InProgress' && <Form.Item name="resolutionNote" label="Ghi chú kết quả" rules={[{ max: 500 }]}><Input.TextArea rows={2} /></Form.Item>}
        </Form>
      </Modal>
    </>
  );
}

// ======================= UC-41 giữ giường =======================
function ReservationsTab({ rooms }: { rooms: Room[] }) {
  const run = useRun();
  const [rows, setRows] = useState<BedReservation[]>([]);
  const [inquiries, setInquiries] = useState<Inquiry[]>([]);
  const [open, setOpen] = useState(false);
  const [form] = Form.useForm();
  const load = useCallback(() => roomOpsApi.reservations().then(setRows).catch(() => undefined), []);
  useEffect(() => {
    void load();
  }, [load]);
  useEffect(() => {
    if (!open) return;
    admissionsApi.inquiries({ filter: ["status ne 'Admitted'", "status ne 'Lost'"], orderBy: 'createdAt desc', top: 50 })
      .then((p) => setInquiries(p.items)).catch(() => undefined);
  }, [open]);

  const freeBeds = rooms.filter((r) => r.isActive).flatMap((r) => r.beds
    .filter((b) => b.status === 'Available' && !b.reservedFor)
    .map((b) => ({ value: b.id, label: `Phòng ${r.number} - Giường ${b.number}${r.dailySurcharge > 0 ? ` (${r.roomTypeName})` : ''}` })));

  const save = async () => {
    const v = await form.validateFields();
    if (await run(() => roomOpsApi.reserve({ ...v, expiresOn: v.expiresOn.format('YYYY-MM-DD') }), 'Đã giữ giường.')) {
      setOpen(false);
      void load();
    }
  };

  return (
    <>
      <Button type="primary" icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => { form.resetFields(); setOpen(true); }}>Giữ giường</Button>
      <Table rowKey="id" size="small" dataSource={rows} pagination={{ pageSize: 20 }} columns={[
        { title: 'Giường', dataIndex: 'bedLabel' },
        { title: 'Giữ cho', render: (_, r) => <>{r.holderName}{r.phone && <><br /><small>{r.phone}</small></>}</> },
        { title: 'Trạng thái', render: (_, r) => <StatusTag kind="reservation" value={r.status} /> },
        { title: 'Đến', render: (_, r) => dateTime(r.expiresAt) },
        { title: 'Ghi chú', dataIndex: 'note' },
        { title: 'Người giữ', dataIndex: 'reservedByName' },
        {
          title: '', key: 'release', render: (_, r) => r.status === 'Active' && (
            <Popconfirm title="Hủy giữ giường?" onConfirm={async () => { if (await run(() => roomOpsApi.release(r.id), 'Đã hủy giữ.')) void load(); }}>
              <Button size="small" danger>Hủy giữ</Button>
            </Popconfirm>
          ),
        },
      ]} />
      <Typography.Paragraph type="secondary" style={{ marginTop: 8 }}>
        Giường đang giữ chỉ xếp được cho cư dân nhập viện từ đúng yêu cầu tư vấn đã giữ; hết hạn thì tự trả về trống.
      </Typography.Paragraph>
      <Modal open={open} title="Giữ giường" okText="Giữ" onOk={save} onCancel={() => setOpen(false)} destroyOnHidden>
        <Form form={form} layout="vertical" initialValues={{ expiresOn: dayjs().add(3, 'day') }}>
          <Form.Item name="bedId" label="Giường trống" rules={[{ required: true, message: 'Chọn giường' }]}><Select options={freeBeds} /></Form.Item>
          <Form.Item name="inquiryId" label="Yêu cầu tư vấn (nên chọn để xếp giường khi nhập viện)">
            <Select allowClear showSearch optionFilterProp="label"
              options={inquiries.map((i) => ({ value: i.id, label: `${i.fullName} — ${i.phone}` }))}
              onChange={(id) => {
                const inquiry = inquiries.find((i) => i.id === id);
                if (inquiry) form.setFieldsValue({ holderName: inquiry.fullName, phone: inquiry.phone });
              }} />
          </Form.Item>
          <Form.Item name="holderName" label="Giữ cho" rules={[{ required: true, max: 150 }]}><Input /></Form.Item>
          <Form.Item name="phone" label="Điện thoại" rules={[{ max: 30 }]}><Input /></Form.Item>
          <Form.Item name="expiresOn" label="Giữ đến hết ngày (tối đa 7 ngày)" rules={[{ required: true }]}>
            <DatePicker format="DD/MM/YYYY" disabledDate={(d) => d.isBefore(dayjs(), 'day') || d.isAfter(dayjs().add(7, 'day'), 'day')} />
          </Form.Item>
          <Form.Item name="note" label="Ghi chú" rules={[{ max: 500 }]}><Input.TextArea rows={2} /></Form.Item>
        </Form>
      </Modal>
    </>
  );
}

// ======================= UC-36 loại phòng & phụ thu =======================
function RoomTypesTab() {
  const run = useRun();
  const [rows, setRows] = useState<RoomType[]>([]);
  const [editing, setEditing] = useState<RoomType | 'new' | null>(null);
  const [form] = Form.useForm();
  const load = useCallback(() => roomOpsApi.roomTypes().then(setRows).catch(() => undefined), []);
  useEffect(() => {
    void load();
  }, [load]);

  const save = async () => {
    const v = await form.validateFields();
    if (await run(() => roomOpsApi.saveRoomType(editing === 'new' ? null : (editing as RoomType).id, v), 'Đã lưu loại phòng.')) {
      setEditing(null);
      void load();
    }
  };

  return (
    <>
      <Button type="primary" icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => { form.resetFields(); form.setFieldsValue({ isActive: true, dailySurcharge: 0 }); setEditing('new'); }}>
        Thêm loại phòng
      </Button>
      <Table rowKey="id" size="small" dataSource={rows} pagination={false} columns={[
        { title: 'Loại phòng', dataIndex: 'name' },
        { title: 'Phụ thu/ngày', render: (_, t) => (t.dailySurcharge > 0 ? money(t.dailySurcharge) : 'Không') },
        { title: 'Mô tả', dataIndex: 'description' },
        { title: 'Đang dùng', render: (_, t) => (t.isActive ? 'Có' : 'Không') },
        { title: '', key: 'edit', render: (_, t) => <Button size="small" onClick={() => { form.setFieldsValue(t); setEditing(t); }}>Sửa</Button> },
      ]} />
      <Typography.Paragraph type="secondary" style={{ marginTop: 8 }}>
        Phụ thu cộng vào hóa đơn tháng theo số ngày cư dân ở phòng loại đó (giá lấy lúc lập hóa đơn). Gán loại cho phòng ở trang Phòng & giường.
      </Typography.Paragraph>
      <Modal open={editing !== null} title={editing === 'new' ? 'Thêm loại phòng' : 'Sửa loại phòng'} okText="Lưu" onOk={save} onCancel={() => setEditing(null)} destroyOnHidden>
        <Form form={form} layout="vertical">
          <Form.Item name="name" label="Tên" rules={[{ required: true, max: 100 }]}><Input /></Form.Item>
          <Form.Item name="dailySurcharge" label="Phụ thu mỗi ngày (đ)" rules={[{ required: true }]}><InputNumber min={0} step={10000} style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="description" label="Mô tả" rules={[{ max: 300 }]}><Input.TextArea rows={2} /></Form.Item>
          <Form.Item name="isActive" label="Đang dùng" valuePropName="checked"><Switch /></Form.Item>
        </Form>
      </Modal>
    </>
  );
}

/** BL-V3: báo hỏng & sửa chữa (mọi nhân viên), giữ giường (lễ tân), loại phòng & phụ thu (Admin). */
export default function RoomOps() {
  const { user } = useAuth();
  const [params, setParams] = useSearchParams();
  const [rooms, setRooms] = useState<Room[]>([]);
  useEffect(() => {
    roomsApi.list().then(setRooms).catch(() => undefined);
  }, []);

  const isAdmin = hasRole(user?.role, ADMIN_ONLY);
  const items = [
    { key: 'maintenance', label: 'Báo hỏng & sửa chữa', children: <MaintenanceTab rooms={rooms} isAdmin={isAdmin} /> },
    ...(hasRole(user?.role, FRONT_DESK) ? [{ key: 'reservations', label: 'Giữ giường', children: <ReservationsTab rooms={rooms} /> }] : []),
    ...(isAdmin ? [{ key: 'types', label: 'Loại phòng & phụ thu', children: <RoomTypesTab /> }] : []),
  ];
  return (
    <>
      <PageTitle title="Vận hành phòng" />
      <Tabs items={items} activeKey={params.get('tab') ?? 'maintenance'} onChange={(tab) => setParams({ tab })} destroyOnHidden />
    </>
  );
}
