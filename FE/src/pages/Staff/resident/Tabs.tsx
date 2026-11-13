import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { App, Button, Checkbox, DatePicker, Form, Input, Modal, Popconfirm, Select, Space, Switch, Table, Tag, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { AccountSelect, StatusTag } from '../../../components/Common';
import { billingApi } from '../../../api/billing';
import { extractErrorMessage } from '../../../api/client';
import { residentsApi, type ContactForm } from '../../../api/residents';
import { roomsApi } from '../../../api/rooms';
import { ServiceUsagePanel } from '../../Operations/ServiceUsages';
import { dateOnly, dateTime, dayjs, money, monthOf } from '../../../utils/format';
import type {
  BedAssignment, Contact, FamilyLink, InvoiceListItem, ResidentDetail, Room, ServicePackage, Subscription,
} from '../../../types';

export { CareTab, MedicationTab, VitalsTab } from './ClinicalTabs';

// ======================= UC-27: Người liên hệ =======================
export function ContactsTab({ residentId, canEdit }: { residentId: number; canEdit: boolean }) {
  const { message } = App.useApp();
  const [rows, setRows] = useState<Contact[]>([]);
  const [editing, setEditing] = useState<Contact | 'new' | null>(null);
  const [form] = Form.useForm<ContactForm>();
  const load = useCallback(() => residentsApi.contacts(residentId).then(setRows).catch(() => undefined), [residentId]);
  useEffect(() => {
    void load();
  }, [load]);

  const save = async () => {
    const v = await form.validateFields();
    try {
      if (editing === 'new') await residentsApi.addContact(residentId, v);
      else if (editing) await residentsApi.updateContact(residentId, editing.id, v);
      setEditing(null);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <>
      {canEdit && <Button icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => { form.resetFields(); setEditing('new'); }}>Thêm người liên hệ</Button>}
      <Table rowKey="id" dataSource={rows} pagination={false} size="small" columns={[
        { title: 'Họ tên', dataIndex: 'fullName' },
        { title: 'Quan hệ', dataIndex: 'relationship' },
        { title: 'Điện thoại', dataIndex: 'phone' },
        { title: 'Email', dataIndex: 'email' },
        { title: 'Khẩn cấp', dataIndex: 'isEmergencyContact', render: (v: boolean) => v && <Tag color="red">Liên hệ khẩn cấp</Tag> },
        ...(canEdit ? [{
          title: '', key: 'a', render: (_: unknown, c: Contact) => (
            <Space>
              <Button size="small" onClick={() => { form.setFieldsValue(c); setEditing(c); }}>Sửa</Button>
              <Popconfirm title="Xóa người liên hệ này?" onConfirm={async () => { await residentsApi.deleteContact(residentId, c.id); void load(); }}>
                <Button size="small" danger>Xóa</Button>
              </Popconfirm>
            </Space>
          ),
        }] : []),
      ]} />
      <Modal open={editing !== null} title="Người liên hệ" onOk={save} onCancel={() => setEditing(null)} okText="Lưu" destroyOnHidden>
        <Form form={form} layout="vertical">
          <Form.Item name="fullName" label="Họ tên" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="relationship" label="Quan hệ" rules={[{ required: true }]}><Input placeholder="Con gái, cháu..." /></Form.Item>
          <Form.Item name="phone" label="Điện thoại" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="email" label="Email" rules={[{ type: 'email' }]}><Input /></Form.Item>
          <Form.Item name="isEmergencyContact" valuePropName="checked"><Checkbox>Liên hệ khẩn cấp</Checkbox></Form.Item>
        </Form>
      </Modal>
    </>
  );
}

// ======================= UC-37..UC-39: Giường =======================
export function BedTab({ resident, onChanged }: { resident: ResidentDetail; onChanged: () => void }) {
  const { message } = App.useApp();
  const [rooms, setRooms] = useState<Room[]>([]);
  const [history, setHistory] = useState<BedAssignment[]>([]);
  const [bedId, setBedId] = useState<number>();
  const load = useCallback(() => {
    roomsApi.list().then(setRooms).catch(() => undefined);
    residentsApi.bedHistory(resident.id).then(setHistory).catch(() => undefined);
  }, [resident.id]);
  useEffect(load, [load]);

  const freeBeds = rooms.filter((r) => r.isActive).flatMap((r) => r.beds.filter((b) => b.status === 'Available').map((b) => ({
    value: b.id, label: `Phòng ${r.number}${r.floor != null ? ` (tầng ${r.floor})` : ''} - Giường ${b.number}`,
  })));

  const run = async (action: () => Promise<unknown>, ok: string) => {
    try {
      await action();
      message.success(ok);
      setBedId(undefined);
      load();
      onChanged();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <>
      {resident.status === 'Active' && (
        <Space wrap style={{ marginBottom: 16 }}>
          <Typography.Text>Giường hiện tại: <b>{resident.bedLabel ?? 'chưa có'}</b></Typography.Text>
          <Select placeholder="Chọn giường trống" style={{ width: 300 }} value={bedId} onChange={setBedId} options={freeBeds} />
          <Button type="primary" disabled={!bedId} onClick={() => run(() => roomsApi.assign(resident.id, bedId!), resident.bedId ? 'Đã chuyển giường.' : 'Đã phân giường.')}>
            {resident.bedId ? 'Chuyển giường' : 'Phân giường'}
          </Button>
          {resident.bedId && (
            <Popconfirm title="Trả giường hiện tại?" onConfirm={() => run(() => residentsApi.releaseBed(resident.id), 'Đã trả giường.')}>
              <Button>Trả giường</Button>
            </Popconfirm>
          )}
        </Space>
      )}
      <Table rowKey="id" size="small" dataSource={history} pagination={false} columns={[
        { title: 'Phòng', dataIndex: 'roomNumber' },
        { title: 'Giường', dataIndex: 'bedNumber' },
        { title: 'Từ', dataIndex: 'startedAt', render: dateTime },
        { title: 'Đến', dataIndex: 'endedAt', render: (v?: string) => (v ? dateTime(v) : <Tag color="blue">Đang ở</Tag>) },
        { title: 'Người phân', dataIndex: 'assignedByName' },
      ]} />
    </>
  );
}

// ======================= UC-28: Tài khoản gia đình & quyền chia sẻ =======================
export function FamilyTab({ residentId }: { residentId: number }) {
  const { message } = App.useApp();
  const [rows, setRows] = useState<FamilyLink[]>([]);
  const [open, setOpen] = useState(false);
  const [form] = Form.useForm();
  const load = useCallback(() => residentsApi.familyLinks(residentId).then(setRows).catch(() => undefined), [residentId]);
  useEffect(() => {
    void load();
  }, [load]);

  const save = async (body: FamilyLink | Record<string, unknown>) => {
    try {
      await residentsApi.saveFamilyLink(residentId, body as FamilyLink);
      setOpen(false);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  const flag = (link: FamilyLink, key: 'canViewHealth' | 'canViewCare' | 'canViewMedication' | 'canViewBilling') => (
    <Switch size="small" checked={link[key]} onChange={(checked) => void save({ ...link, [key]: checked })} />
  );

  return (
    <>
      <Button icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => { form.resetFields(); setOpen(true); }}>Liên kết tài khoản gia đình</Button>
      <Table rowKey="id" size="small" dataSource={rows} pagination={false} columns={[
        { title: 'Tài khoản', render: (_, l) => <>{l.familyName}<br /><small>{l.familyEmail}</small></> },
        { title: 'Sức khỏe', render: (_, l) => flag(l, 'canViewHealth') },
        { title: 'Chăm sóc', render: (_, l) => flag(l, 'canViewCare') },
        { title: 'Thuốc', render: (_, l) => flag(l, 'canViewMedication') },
        { title: 'Hóa đơn', render: (_, l) => flag(l, 'canViewBilling') },
        {
          title: '', key: 'del', render: (_, l) => (
            <Popconfirm title="Gỡ liên kết?" onConfirm={async () => { await residentsApi.deleteFamilyLink(residentId, l.id); void load(); }}>
              <Button size="small" danger>Gỡ</Button>
            </Popconfirm>
          ),
        },
      ]} />
      <Typography.Paragraph type="secondary" style={{ marginTop: 8 }}>
        Sinh hiệu và ghi chép chăm sóc chỉ hiện với gia đình khi bật quyền nhóm VÀ nhân viên đánh dấu "chia sẻ" ở từng bản ghi.
      </Typography.Paragraph>
      <Modal open={open} title="Liên kết tài khoản gia đình" onOk={async () => save(await form.validateFields())} onCancel={() => setOpen(false)} okText="Liên kết" destroyOnHidden>
        <Form form={form} layout="vertical" initialValues={{ canViewHealth: true, canViewCare: true, canViewMedication: false, canViewBilling: true }}>
          <Form.Item name="familyAccountId" label="Tài khoản Family" rules={[{ required: true }]}><AccountSelect roles={['Family']} /></Form.Item>
          <Form.Item name="canViewHealth" valuePropName="checked"><Checkbox>Xem sức khỏe</Checkbox></Form.Item>
          <Form.Item name="canViewCare" valuePropName="checked"><Checkbox>Xem chăm sóc</Checkbox></Form.Item>
          <Form.Item name="canViewMedication" valuePropName="checked"><Checkbox>Xem thuốc</Checkbox></Form.Item>
          <Form.Item name="canViewBilling" valuePropName="checked"><Checkbox>Xem & thanh toán hóa đơn</Checkbox></Form.Item>
        </Form>
      </Modal>
    </>
  );
}

// ======================= UC-84, UC-87: Gói & hóa đơn của cư dân =======================
export function BillingTab({ residentId, active }: { residentId: number; active: boolean }) {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const [subs, setSubs] = useState<Subscription[]>([]);
  const [invoices, setInvoices] = useState<InvoiceListItem[]>([]);
  const [packages, setPackages] = useState<ServicePackage[]>([]);
  const [subForm] = Form.useForm();
  const [invoiceForm] = Form.useForm();
  const [mode, setMode] = useState<'sub' | 'invoice' | null>(null);

  const load = useCallback(() => {
    residentsApi.subscriptions(residentId).then(setSubs).catch(() => undefined);
    billingApi.invoices({ filter: [`residentId eq ${residentId}`], orderBy: 'billingPeriod desc' }).then((p) => setInvoices(p.items)).catch(() => undefined);
  }, [residentId]);
  useEffect(() => {
    load();
    billingApi.packages(true).then(setPackages).catch(() => undefined);
  }, [load]);

  const subscribe = async () => {
    const v = await subForm.validateFields();
    try {
      await billingApi.subscribe({ residentId, servicePackageId: v.servicePackageId, startDate: v.startDate.format('YYYY-MM-DD'), endDate: v.endDate?.format('YYYY-MM-DD') });
      setMode(null);
      load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  const createInvoice = async () => {
    const v = await invoiceForm.validateFields();
    try {
      const period = v.period.startOf('month');
      const invoice = await billingApi.createInvoice({ residentId, billingPeriod: period.format('YYYY-MM-DD'), dueDate: v.dueDate.format('YYYY-MM-DD') });
      navigate(`/staff/invoices/${invoice.id}`);
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <>
      <Space style={{ marginBottom: 12 }}>
        {active && <Button onClick={() => { subForm.resetFields(); subForm.setFieldsValue({ startDate: dayjs() }); setMode('sub'); }}>Đăng ký gói</Button>}
        <Button type="primary" onClick={() => { invoiceForm.resetFields(); invoiceForm.setFieldsValue({ period: dayjs().subtract(1, 'month'), dueDate: dayjs().date(10) }); setMode('invoice'); }}>Tạo hóa đơn tháng</Button>
      </Space>
      <Typography.Title level={5}>Gói dịch vụ đã đăng ký</Typography.Title>
      <Table rowKey="id" size="small" dataSource={subs} pagination={false} columns={[
        { title: 'Gói', dataIndex: 'packageName' },
        { title: 'Giá chốt/tháng', dataIndex: 'agreedMonthlyPrice', render: money },
        { title: 'Từ', dataIndex: 'startDate', render: dateOnly },
        { title: 'Đến', dataIndex: 'endDate', render: (v?: string) => (v ? dateOnly(v) : <Tag color="green">Đang dùng</Tag>) },
        {
          title: '', key: 'end', render: (_, s) => !s.endDate && (
            <Popconfirm title="Kết thúc gói hôm nay?" onConfirm={async () => { try { await billingApi.endSubscription(s.id); load(); } catch (e) { message.error(extractErrorMessage(e)); } }}>
              <Button size="small">Kết thúc</Button>
            </Popconfirm>
          ),
        },
      ]} />
      <Typography.Title level={5} style={{ marginTop: 16 }}>Hóa đơn</Typography.Title>
      <Table rowKey="id" size="small" dataSource={invoices} pagination={false}
        onRow={(r) => ({ onClick: () => navigate(`/staff/invoices/${r.id}`), style: { cursor: 'pointer' } })}
        columns={[
          { title: 'Số', dataIndex: 'number' },
          { title: 'Kỳ', dataIndex: 'billingPeriod', render: monthOf },
          { title: 'Tổng', dataIndex: 'total', render: money },
          { title: 'Còn nợ', dataIndex: 'remaining', render: money },
          { title: 'Trạng thái', dataIndex: 'status', render: (v: string) => <StatusTag kind="invoice" value={v} /> },
        ]} />
      <ServiceUsagePanel residentId={residentId} active={active} />
      <Modal open={mode === 'sub'} title="Đăng ký gói (giá chốt theo giá hiện tại)" onOk={subscribe} onCancel={() => setMode(null)} destroyOnHidden>
        <Form form={subForm} layout="vertical">
          <Form.Item name="servicePackageId" label="Gói" rules={[{ required: true }]}>
            <Select options={packages.map((p) => ({ value: p.id, label: `${p.name} — ${money(p.monthlyPrice)}` }))} />
          </Form.Item>
          <Form.Item name="startDate" label="Bắt đầu" rules={[{ required: true }]}><DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="endDate" label="Kết thúc (bỏ trống nếu chưa biết)"><DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} /></Form.Item>
        </Form>
      </Modal>
      <Modal open={mode === 'invoice'} title="Tạo hóa đơn tháng (tính theo số ngày lưu trú)" onOk={createInvoice} onCancel={() => setMode(null)} destroyOnHidden>
        <Form form={invoiceForm} layout="vertical">
          <Form.Item name="period" label="Kỳ tính phí" rules={[{ required: true }]}><DatePicker picker="month" format="MM/YYYY" style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="dueDate" label="Hạn thanh toán" rules={[{ required: true }]}><DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} /></Form.Item>
        </Form>
      </Modal>
    </>
  );
}
