import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { App, Button, DatePicker, Form, Input, InputNumber, Modal, Popconfirm, Select, Space, Table, Tag, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import { billingExtrasApi } from '../../api/billingExtras';
import { extractErrorMessage } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { BILLING_STAFF, hasRole } from '../../auth/roles';
import { PageTitle, ResidentSelect } from '../../components/Common';
import { dateOnly, dayjs, money, todayVn } from '../../utils/format';
import type { ExtraService, ServiceUsage } from '../../types/operations';

const MAX_BACKDATE_DAYS = 62;

/** UC-86: trang ghi nhanh dịch vụ phát sinh cho mọi nhân viên ghi được (Nurse/Caregiver không có tab Gói & hóa đơn). */
export default function ServiceUsages() {
  const [month, setMonth] = useState<Dayjs>(() => dayjs(todayVn()).startOf('month'));
  const [adding, setAdding] = useState(false);
  const [version, setVersion] = useState(0);

  return (
    <>
      <PageTitle
        title="Ghi dịch vụ phát sinh"
        extra={<Button type="primary" icon={<PlusOutlined />} onClick={() => setAdding(true)}>Ghi sử dụng</Button>}
      />
      <Space style={{ marginBottom: 16 }} wrap>
        <DatePicker picker="month" format="MM/YYYY" allowClear={false} value={month} onChange={(d) => d && setMonth(d.startOf('month'))} />
        <Typography.Text type="secondary">Lần chưa tính tiền sẽ tự vào hóa đơn tháng của cư dân, theo đơn giá lúc ghi.</Typography.Text>
      </Space>
      <UsageTable key={`${month.format('YYYY-MM')}-${version}`} from={month.format('YYYY-MM-DD')} to={month.endOf('month').format('YYYY-MM-DD')} showResident />
      {adding && <UsageModal onClose={() => setAdding(false)} onSaved={() => { setAdding(false); setVersion((v) => v + 1); }} />}
    </>
  );
}

/** Khối "Dịch vụ phát sinh tháng này" trong tab Gói & hóa đơn của hồ sơ cư dân. */
export function ServiceUsagePanel({ residentId, active }: { residentId: number; active: boolean }) {
  const [adding, setAdding] = useState(false);
  const [version, setVersion] = useState(0);
  const today = dayjs(todayVn());
  return (
    <>
      <Space style={{ margin: '16px 0 8px' }}>
        <Typography.Title level={5} style={{ margin: 0 }}>Dịch vụ phát sinh tháng này</Typography.Title>
        {active && <Button size="small" icon={<PlusOutlined />} onClick={() => setAdding(true)}>Ghi sử dụng</Button>}
      </Space>
      <UsageTable key={version} residentId={residentId} from={today.startOf('month').format('YYYY-MM-DD')} to={today.format('YYYY-MM-DD')} />
      {adding && <UsageModal residentId={residentId} onClose={() => setAdding(false)} onSaved={() => { setAdding(false); setVersion((v) => v + 1); }} />}
    </>
  );
}

function UsageTable({ residentId, from, to, showResident }: { residentId?: number; from: string; to: string; showResident?: boolean }) {
  const { message } = App.useApp();
  const { user } = useAuth();
  const deletesAny = hasRole(user?.role, BILLING_STAFF);
  const [rows, setRows] = useState<ServiceUsage[]>([]);
  const [loading, setLoading] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(residentId ? await billingExtrasApi.residentUsages(residentId, from, to) : await billingExtrasApi.usages(from, to));
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [residentId, from, to, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const remove = async (row: ServiceUsage) => {
    try {
      await billingExtrasApi.deleteUsage(row.id);
      message.success('Đã xóa lần sử dụng.');
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  const total = rows.reduce((sum, row) => sum + row.amount, 0);

  return (
    <Table<ServiceUsage>
      rowKey="id"
      size="small"
      loading={loading}
      dataSource={rows}
      pagination={{ pageSize: 20, hideOnSinglePage: true }}
      scroll={{ x: 900 }}
      footer={rows.length ? () => <Typography.Text strong>Tổng: {money(total)}</Typography.Text> : undefined}
      columns={[
        { title: 'Ngày', dataIndex: 'usedOn', width: 110, render: dateOnly },
        ...(showResident ? [{
          title: 'Cư dân', key: 'resident', render: (_: unknown, u: ServiceUsage) => <Link to={`/staff/residents/${u.residentId}`}>{u.residentCode} · {u.residentName}</Link>,
        }] : []),
        { title: 'Dịch vụ', render: (_, u) => <>{u.serviceName}{u.note ? <Typography.Text type="secondary"> — {u.note}</Typography.Text> : null}</> },
        { title: 'Số lượng', align: 'right', render: (_, u) => `${u.quantity.toLocaleString('vi-VN')} ${u.unit}` },
        { title: 'Đơn giá', dataIndex: 'unitPrice', align: 'right', render: money },
        { title: 'Thành tiền', dataIndex: 'amount', align: 'right', render: money },
        { title: 'Người ghi', dataIndex: 'recordedByName' },
        {
          title: 'Hóa đơn', render: (_, u) => (u.invoiced
            ? (deletesAny && u.invoiceId ? <Link to={`/staff/invoices/${u.invoiceId}`}><Tag color="blue">Đã vào hóa đơn</Tag></Link> : <Tag color="blue">Đã vào hóa đơn</Tag>)
            : <Tag>Chưa tính</Tag>),
        },
        {
          title: '', width: 70, render: (_, u) => (!u.invoiced && (deletesAny || u.recordedById === user?.accountId) ? (
            <Popconfirm title="Xóa lần sử dụng này?" okText="Xóa" cancelText="Không" onConfirm={() => remove(u)}>
              <Button size="small" danger>Xóa</Button>
            </Popconfirm>
          ) : null),
        },
      ]}
    />
  );
}

function UsageModal({ residentId, onClose, onSaved }: { residentId?: number; onClose: () => void; onSaved: () => void }) {
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const [services, setServices] = useState<ExtraService[]>([]);
  const [saving, setSaving] = useState(false);
  const serviceId = Form.useWatch('extraServiceId', form) as number | undefined;
  const quantity = Form.useWatch('quantity', form) as number | undefined;
  const selected = services.find((s) => s.id === serviceId);

  useEffect(() => {
    billingExtrasApi.services(true).then(setServices).catch((e) => message.error(extractErrorMessage(e)));
  }, [message]);

  const save = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      await billingExtrasApi.recordUsage(residentId ?? values.residentId, {
        extraServiceId: values.extraServiceId,
        quantity: values.quantity,
        usedOn: (values.usedOn as Dayjs).format('YYYY-MM-DD'),
        note: values.note,
      });
      message.success('Đã ghi lần sử dụng.');
      onSaved();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  const today = dayjs(todayVn());
  return (
    <Modal open title="Ghi dịch vụ phát sinh" onOk={save} onCancel={onClose} okText="Ghi" confirmLoading={saving}>
      <Form form={form} layout="vertical" initialValues={{ quantity: 1, usedOn: today }}>
        {!residentId && (
          <Form.Item name="residentId" label="Cư dân" rules={[{ required: true, message: 'Chọn cư dân' }]}><ResidentSelect /></Form.Item>
        )}
        <Form.Item name="extraServiceId" label="Dịch vụ" rules={[{ required: true, message: 'Chọn dịch vụ' }]}>
          <Select showSearch optionFilterProp="label" placeholder="Chọn dịch vụ"
            options={services.map((s) => ({ value: s.id, label: `${s.name} — ${money(s.unitPrice)}/${s.unit}` }))} />
        </Form.Item>
        <Space wrap>
          <Form.Item name="quantity" label={`Số lượng${selected ? ` (${selected.unit})` : ''}`} rules={[{ required: true }]}>
            <InputNumber min={0.01} max={9999} step={0.5} precision={2} />
          </Form.Item>
          <Form.Item name="usedOn" label="Ngày sử dụng" rules={[{ required: true }]}>
            <DatePicker format="DD/MM/YYYY" allowClear={false}
              disabledDate={(d) => d.isAfter(today, 'day') || d.isBefore(today.subtract(MAX_BACKDATE_DAYS, 'day'), 'day')} />
          </Form.Item>
        </Space>
        <Form.Item name="note" label="Ghi chú"><Input maxLength={500} /></Form.Item>
        {selected && quantity ? (
          <Typography.Text type="secondary">
            Tạm tính: {money(Math.round(selected.unitPrice * quantity))} (đơn giá chốt {money(selected.unitPrice)}/{selected.unit}).
          </Typography.Text>
        ) : null}
      </Form>
    </Modal>
  );
}
