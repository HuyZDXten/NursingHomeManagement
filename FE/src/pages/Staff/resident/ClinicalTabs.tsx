import { useCallback, useEffect, useState } from 'react';
import { Alert, App, Button, Checkbox, DatePicker, Form, Input, InputNumber, Modal, Popconfirm, Space, Table, Tag, TimePicker, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { aiInsightsApi } from '../../../api/aiInsights';
import { careApi, type VitalForm } from '../../../api/care';
import { extractErrorMessage } from '../../../api/client';
import { incidentsApi } from '../../../api/incidents';
import { medicalProfileApi } from '../../../api/medicalProfile';
import { residentsApi } from '../../../api/residents';
import { useAuth } from '../../../auth/AuthContext';
import { CARE_PLANNERS, PRESCRIBERS, hasRole } from '../../../auth/roles';
import { CreateTaskModal, TaskRecordsDrawer, UpdateTaskModal } from '../../../components/CareTaskModals';
import { IncidentDetailDrawer, ReportIncidentModal } from '../../../components/IncidentModals';
import { StatusTag } from '../../../components/Common';
import { VitalsChart } from '../../../components/VitalsChart';
import { VitalThresholdCard } from './AssessmentTabs';
import { dateOnly, dateTime, dayjs, hhmm } from '../../../utils/format';
import type { CareTask, MedicationLog, MedicationSchedule, VitalSign } from '../../../types';
import type { AiInsight, Incident } from '../../../types/care';

// ======================= UC-50..UC-55: Chăm sóc của cư dân =======================
export function CareTab({ residentId }: { residentId: number }) {
  const { user } = useAuth();
  const [rows, setRows] = useState<CareTask[]>([]);
  const [creating, setCreating] = useState(false);
  const [updating, setUpdating] = useState<CareTask | null>(null);
  const [viewing, setViewing] = useState<CareTask | null>(null);
  const load = useCallback(() => careApi.tasks({ params: { residentId }, orderBy: 'dueAt desc', top: 50 }).then((p) => setRows(p.items)).catch(() => undefined), [residentId]);
  useEffect(() => {
    void load();
  }, [load]);

  return (
    <>
      {hasRole(user?.role, CARE_PLANNERS) && <Button icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => setCreating(true)}>Giao việc</Button>}
      <Table rowKey="id" size="small" dataSource={rows} pagination={{ pageSize: 10 }} columns={[
        { title: 'Hạn', dataIndex: 'dueAt', render: dateTime },
        { title: 'Công việc', dataIndex: 'title' },
        { title: 'Người làm', dataIndex: 'assignedToName' },
        { title: 'Trạng thái', dataIndex: 'status', render: (v: string) => <StatusTag kind="care" value={v} /> },
        {
          title: '', key: 'a', render: (_, t) => (
            <Space>
              {(t.status === 'Pending' || t.status === 'InProgress') && <Button size="small" onClick={() => setUpdating(t)}>Cập nhật</Button>}
              <Button size="small" onClick={() => setViewing(t)}>Lịch sử</Button>
            </Space>
          ),
        },
      ]} />
      <CreateTaskModal open={creating} residentId={residentId} onClose={() => setCreating(false)} onSaved={() => { setCreating(false); void load(); }} />
      <UpdateTaskModal task={updating} onClose={() => setUpdating(null)} onSaved={() => { setUpdating(null); void load(); }} />
      <TaskRecordsDrawer task={viewing} onClose={() => setViewing(null)} />
    </>
  );
}

// ======================= UC-62, UC-63: Sinh hiệu =======================
export function VitalsTab({ residentId, canRecord }: { residentId: number; canRecord: boolean }) {
  const { message } = App.useApp();
  const [rows, setRows] = useState<VitalSign[]>([]);
  const [trends, setTrends] = useState<AiInsight[]>([]);
  const [open, setOpen] = useState(false);
  const [form] = Form.useForm<VitalForm>();
  const load = useCallback(() => {
    residentsApi.vitals(residentId, dayjs().subtract(30, 'day').format('YYYY-MM-DD')).then(setRows).catch(() => undefined);
    aiInsightsApi.vitalTrends(residentId).then(setTrends).catch(() => setTrends([]));
  }, [residentId]);
  useEffect(() => {
    void load();
  }, [load]);

  const save = async () => {
    const v = await form.validateFields();
    try {
      await careApi.recordVital({ ...v, residentId });
      message.success('Đã ghi sinh hiệu. Giá trị vượt ngưỡng của cơ sở sẽ được báo cho điều dưỡng/bác sĩ.');
      setOpen(false);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <>
      <VitalThresholdCard residentId={residentId} canEdit={canRecord} />
      {trends[0] && (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 12 }}
          title={
            <Space size={6}>
              <span>Cảnh báo xu hướng sinh hiệu</span>
              <Tag color="gold">Do AI soạn</Tag>
            </Space>
          }
          description={
            <>
              <div>{trends[0].content}</div>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {trends[0].disclaimer}
              </Typography.Text>
            </>
          }
        />
      )}
      {canRecord && <Button icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => { form.resetFields(); setOpen(true); }}>Ghi sinh hiệu</Button>}
      <VitalsChart vitals={rows} />
      <Table rowKey="id" size="small" dataSource={rows} pagination={{ pageSize: 10 }} style={{ marginTop: 12 }} columns={[
        { title: 'Lúc', dataIndex: 'recordedAt', render: dateTime },
        { title: 'Huyết áp', render: (_, v) => `${v.systolic}/${v.diastolic}` },
        { title: 'Mạch', dataIndex: 'heartRate' },
        { title: 'Nhiệt độ', dataIndex: 'temperature', render: (t: number) => `${t}°C` },
        { title: 'SpO2', dataIndex: 'spO2', render: (s?: number) => (s != null ? `${s}%` : '') },
        { title: 'Ghi chú', dataIndex: 'notes' },
        { title: 'Người ghi', dataIndex: 'recordedByName' },
        { title: '', dataIndex: 'shareWithFamily', render: (s: boolean) => s && <Tag color="purple">Gia đình xem được</Tag> },
      ]} />
      <Modal open={open} title="Ghi sinh hiệu" onOk={save} onCancel={() => setOpen(false)} okText="Lưu" destroyOnHidden>
        <Form form={form} layout="vertical" initialValues={{ shareWithFamily: true }}>
          <Space wrap>
            <Form.Item name="systolic" label="Tâm thu" rules={[{ required: true }]}><InputNumber min={40} max={300} /></Form.Item>
            <Form.Item name="diastolic" label="Tâm trương" rules={[{ required: true }]}><InputNumber min={20} max={200} /></Form.Item>
            <Form.Item name="heartRate" label="Mạch" rules={[{ required: true }]}><InputNumber min={20} max={250} /></Form.Item>
            <Form.Item name="temperature" label="Nhiệt độ (°C)" rules={[{ required: true }]}><InputNumber min={25} max={45} step={0.1} /></Form.Item>
            <Form.Item name="spO2" label="SpO2 (%)"><InputNumber min={0} max={100} /></Form.Item>
          </Space>
          <Form.Item name="notes" label="Ghi chú"><Input.TextArea rows={2} /></Form.Item>
          <Form.Item name="shareWithFamily" valuePropName="checked"><Checkbox>Chia sẻ với gia đình</Checkbox></Form.Item>
          <Typography.Text type="secondary">Khoảng giá trị chỉ là kiểm tra nhập liệu, không phải ngưỡng chẩn đoán.</Typography.Text>
        </Form>
      </Modal>
    </>
  );
}

// ======================= UC-73..UC-78: Thuốc =======================
export function MedicationTab({ residentId, active }: { residentId: number; active: boolean }) {
  const { message, modal } = App.useApp();
  const { user } = useAuth();
  const canPrescribe = hasRole(user?.role, PRESCRIBERS);
  const [schedules, setSchedules] = useState<MedicationSchedule[]>([]);
  const [logs, setLogs] = useState<MedicationLog[]>([]);
  const [open, setOpen] = useState(false);
  const [form] = Form.useForm();
  const load = useCallback(() => {
    careApi.schedules(residentId).then(setSchedules).catch(() => undefined);
    residentsApi.medicationLogs(residentId).then(setLogs).catch(() => undefined);
  }, [residentId]);
  useEffect(load, [load]);

  const save = async () => {
    const v = await form.validateFields();
    const payload = {
      ...v,
      residentId,
      startDate: v.startDate.format('YYYY-MM-DD'),
      endDate: v.endDate?.format('YYYY-MM-DD'),
      scheduledTime: v.scheduledTime.format('HH:mm:00'),
    };
    try {
      const warnings = await medicalProfileApi.checkMedication(residentId, v.medicationName);
      if (warnings.length > 0) {
        let acknowledged = false;
        modal.confirm({
          title: 'Cảnh báo an toàn thuốc',
          okText: 'Xác nhận lưu lịch thuốc',
          cancelText: 'Hủy',
          content: (
            <div>
              <ul style={{ paddingLeft: 18, marginBottom: 12 }}>
                {warnings.map((w, idx) => (
                  <li key={idx} style={{ color: w.kind === 'Allergy' ? '#cf1322' : '#d46b08' }}>{w.message}</li>
                ))}
              </ul>
              <Checkbox onChange={(e) => { acknowledged = e.target.checked; }}>
                Tôi đã xem cảnh báo
              </Checkbox>
              <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0, fontSize: 12 }}>
                Cảnh báo theo quy tắc so khớp tên, không thay thế kiểm tra của bác sĩ.
              </Typography.Paragraph>
            </div>
          ),
          onOk: async () => {
            if (!acknowledged) {
              message.warning('Vui lòng tick "Tôi đã xem cảnh báo" trước khi lưu.');
              return Promise.reject();
            }
            await careApi.createSchedule({ ...payload, acknowledgeWarnings: true });
            setOpen(false);
            load();
          },
        });
        return;
      }

      await careApi.createSchedule(payload);
      setOpen(false);
      load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <>
      {canPrescribe && active && <Button icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => { form.resetFields(); form.setFieldsValue({ startDate: dayjs(), route: 'Uống' }); setOpen(true); }}>Nhập lịch thuốc theo chỉ định</Button>}
      <Table rowKey="id" size="small" dataSource={schedules} pagination={false} columns={[
        { title: 'Thuốc', dataIndex: 'medicationName' },
        { title: 'Liều', dataIndex: 'dose' },
        { title: 'Đường dùng', dataIndex: 'route' },
        { title: 'Giờ', dataIndex: 'scheduledTime', render: hhmm },
        { title: 'Từ – đến', render: (_, s) => `${dateOnly(s.startDate)} – ${s.endDate ? dateOnly(s.endDate) : '...'}` },
        { title: 'Chỉ định', dataIndex: 'approvalReference' },
        { title: '', dataIndex: 'isActive', render: (a: boolean) => (a ? <Tag color="green">Đang dùng</Tag> : <Tag>Đã dừng</Tag>) },
        ...(canPrescribe ? [{
          title: '', key: 'stop', render: (_: unknown, s: MedicationSchedule) => s.isActive && (
            <Popconfirm title="Dừng lịch này? Muốn đổi liều: dừng rồi nhập lịch mới." onConfirm={async () => { await careApi.stopSchedule(s.id); load(); }}>
              <Button size="small" danger>Dừng</Button>
            </Popconfirm>
          ),
        }] : []),
      ]} />
      <Typography.Title level={5} style={{ marginTop: 16 }}>Lịch sử dùng thuốc</Typography.Title>
      <Table rowKey="id" size="small" dataSource={logs} pagination={{ pageSize: 10 }} columns={[
        { title: 'Giờ dự kiến', dataIndex: 'scheduledAt', render: dateTime },
        { title: 'Thuốc', dataIndex: 'medicationName' },
        { title: 'Kết quả', dataIndex: 'status', render: (v: string) => <StatusTag kind="medication" value={v} /> },
        { title: 'Ghi chú', dataIndex: 'notes' },
        { title: 'Người ghi', dataIndex: 'recordedByName' },
      ]} />
      <Modal open={open} title="Nhập lịch thuốc theo chỉ định" onOk={save} onCancel={() => setOpen(false)} okText="Lưu" destroyOnHidden>
        <Typography.Paragraph type="warning">Hệ thống không kê đơn, không đề xuất liều. Chỉ nhập đúng theo chỉ định đã duyệt.</Typography.Paragraph>
        <Form form={form} layout="vertical">
          <Form.Item name="medicationName" label="Tên thuốc" rules={[{ required: true }]}><Input /></Form.Item>
          <Space wrap>
            <Form.Item name="dose" label="Liều" rules={[{ required: true }]}><Input /></Form.Item>
            <Form.Item name="route" label="Đường dùng" rules={[{ required: true }]}><Input /></Form.Item>
            <Form.Item name="scheduledTime" label="Giờ dùng" rules={[{ required: true }]}><TimePicker format="HH:mm" minuteStep={5} /></Form.Item>
          </Space>
          <Space wrap>
            <Form.Item name="startDate" label="Từ ngày" rules={[{ required: true }]}><DatePicker format="DD/MM/YYYY" /></Form.Item>
            <Form.Item name="endDate" label="Đến ngày"><DatePicker format="DD/MM/YYYY" /></Form.Item>
          </Space>
          <Form.Item name="approvalReference" label="Số chỉ định / phiếu duyệt" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="instructions" label="Hướng dẫn"><Input.TextArea rows={2} /></Form.Item>
        </Form>
      </Modal>
    </>
  );
}

// ======================= S3-H1 (UC-58, UC-59): Sự cố của cư dân =======================
export function IncidentsTab({ residentId, active }: { residentId: number; active: boolean }) {
  const [rows, setRows] = useState<Incident[]>([]);
  const [reporting, setReporting] = useState(false);
  const [selected, setSelected] = useState<Incident | null>(null);
  const load = useCallback(() => incidentsApi.list({ residentId }).then(setRows).catch(() => undefined), [residentId]);
  useEffect(() => {
    void load();
  }, [load]);

  return (
    <>
      {active && (
        <Button icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => setReporting(true)}>
          Báo sự cố
        </Button>
      )}
      <Table
        rowKey="id"
        size="small"
        dataSource={rows}
        pagination={{ pageSize: 10 }}
        onRow={(r) => ({ onClick: () => setSelected(r), style: { cursor: 'pointer' } })}
        columns={[
          { title: 'Thời điểm', dataIndex: 'occurredAt', render: dateTime },
          { title: 'Loại', dataIndex: 'type', render: (v: string) => <StatusTag kind="incidentType" value={v} /> },
          { title: 'Mức độ', dataIndex: 'severity', render: (v: string) => <StatusTag kind="incidentSeverity" value={v} /> },
          { title: 'Mô tả', dataIndex: 'description', ellipsis: true },
          { title: 'Trạng thái', dataIndex: 'status', render: (v: string) => <StatusTag kind="incident" value={v} /> },
          {
            title: '',
            key: 'a',
            render: (_, r) => <Button size="small" onClick={(e) => { e.stopPropagation(); setSelected(r); }}>Chi tiết</Button>,
          },
        ]}
      />
      <ReportIncidentModal
        open={reporting}
        residentId={residentId}
        onClose={() => setReporting(false)}
        onSaved={() => { setReporting(false); void load(); }}
      />
      <IncidentDetailDrawer
        incident={selected}
        onClose={() => setSelected(null)}
        onUpdated={(next) => { setSelected(next); void load(); }}
      />
    </>
  );
}
