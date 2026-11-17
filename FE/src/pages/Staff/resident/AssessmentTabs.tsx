import { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  App,
  Button,
  Card,
  Checkbox,
  DatePicker,
  Descriptions,
  Form,
  Input,
  InputNumber,
  Modal,
  Radio,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import { PlusOutlined, SettingOutlined } from '@ant-design/icons';
import { extractErrorMessage } from '../../../api/client';
import { clinicalApi, type SaveThresholdForm } from '../../../api/clinical';
import { useAuth } from '../../../auth/AuthContext';
import { CARE_PLANNERS, PRESCRIBERS, hasRole } from '../../../auth/roles';
import { StatusTag } from '../../../components/Common';
import { dateOnly, dateTime, dayjs } from '../../../utils/format';
import type { Assessment, AssessmentScale, AssessmentType, DoctorVisit, VitalThreshold } from '../../../types/care';

// ======================= S3-H5 (UC-64): Đánh giá định kỳ =======================
export function AssessmentsTab({ residentId, active }: { residentId: number; active: boolean }) {
  const { message } = App.useApp();
  const { user } = useAuth();
  const canAssess = active && hasRole(user?.role, [...CARE_PLANNERS, 'Doctor']);

  const [rows, setRows] = useState<Assessment[]>([]);
  const [scales, setScales] = useState<AssessmentScale[]>([]);
  const [open, setOpen] = useState(false);
  const [selectedType, setSelectedType] = useState<AssessmentType>('FallRisk');
  const [submitting, setSubmitting] = useState(false);
  const [form] = Form.useForm();

  const load = useCallback(() => {
    clinicalApi.listAssessments(residentId).then(setRows).catch(() => undefined);
  }, [residentId]);

  useEffect(() => {
    void load();
  }, [load]);

  const openModal = async () => {
    let list = scales;
    if (list.length === 0) {
      list = await clinicalApi.scales().catch(() => []);
      setScales(list);
    }
    form.resetFields();
    setSelectedType('FallRisk');
    setOpen(true);
  };

  const currentScale = scales.find((s) => s.type === selectedType);

  const submit = async () => {
    const v = await form.validateFields();
    setSubmitting(true);
    try {
      const saved = await clinicalApi.submitAssessment({
        residentId,
        type: selectedType,
        answers: v.answers ?? {},
        note: v.note,
      });
      message.success(`Đã lưu đánh giá: ${saved.score}/${saved.maxScore} điểm (${saved.riskLevel}).`);
      setOpen(false);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 12 }}
        title="Công cụ sàng lọc, không thay thế chẩn đoán."
      />
      {canAssess && (
        <Button type="primary" icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={openModal}>
          Đánh giá mới
        </Button>
      )}

      <Table
        rowKey="id"
        size="small"
        dataSource={rows}
        pagination={{ pageSize: 10 }}
        columns={[
          { title: 'Ngày đánh giá', dataIndex: 'assessedAt', render: dateTime },
          { title: 'Thang đánh giá', dataIndex: 'type', render: (v: string) => <StatusTag kind="assessmentType" value={v} /> },
          { title: 'Điểm', render: (_, a) => `${a.score} / ${a.maxScore}` },
          { title: 'Mức nguy cơ', dataIndex: 'riskLevel', render: (v: string) => <StatusTag kind="riskLevel" value={v} /> },
          { title: 'Hạn đánh giá lại', dataIndex: 'nextDueOn', render: (d?: string) => (d ? dateOnly(d) : '—') },
          { title: 'Người đánh giá', dataIndex: 'assessedByName' },
          { title: 'Ghi chú', dataIndex: 'note', ellipsis: true },
        ]}
      />

      <Modal
        open={open}
        title="Thực hiện đánh giá sàng lọc"
        onOk={submit}
        onCancel={() => setOpen(false)}
        confirmLoading={submitting}
        width={680}
        okText="Lưu kết quả"
        destroyOnHidden
      >
        <Alert type="warning" showIcon style={{ marginBottom: 12 }} title="Công cụ sàng lọc, không thay thế chẩn đoán." />
        <Form form={form} layout="vertical">
          <Form.Item label="Chọn thang đánh giá">
            <Select
              value={selectedType}
              onChange={(val) => {
                setSelectedType(val);
                form.resetFields(['answers']);
              }}
              options={scales.map((s) => ({ value: s.type, label: `${s.title} (tối đa ${s.maxScore}đ)` }))}
            />
          </Form.Item>

          {currentScale?.questions.map((q) => (
            <Form.Item
              key={q.key}
              name={['answers', q.key]}
              label={q.label}
              rules={[{ required: true, message: 'Chọn một mức phù hợp' }]}
            >
              <Radio.Group>
                <Space orientation="vertical">
                  {q.options.map((o) => (
                    <Radio key={o.key} value={o.key}>
                      {o.label} ({o.points}đ)
                    </Radio>
                  ))}
                </Space>
              </Radio.Group>
            </Form.Item>
          ))}

          <Form.Item name="note" label="Ghi chú thêm">
            <Input.TextArea rows={2} maxLength={500} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}

// ======================= S3-H5 (UC-67): Phiếu khám bác sĩ (SOAP) =======================
export function DoctorVisitsTab({ residentId, active }: { residentId: number; active: boolean }) {
  const { message } = App.useApp();
  const { user } = useAuth();
  const isDoctor = user?.role === 'Doctor';

  const [rows, setRows] = useState<DoctorVisit[]>([]);
  const [editing, setEditing] = useState<DoctorVisit | 'new' | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [form] = Form.useForm();

  const load = useCallback(() => {
    clinicalApi.listDoctorVisits(residentId).then(setRows).catch(() => undefined);
  }, [residentId]);

  useEffect(() => {
    void load();
  }, [load]);

  const canEditVisit = (v: DoctorVisit) =>
    active && isDoctor && v.doctorId === user?.accountId && dayjs().diff(dayjs(v.createdAt), 'hour', true) <= 24;

  const save = async () => {
    const v = await form.validateFields();
    setSubmitting(true);
    try {
      const payload = {
        visitedAt: v.visitedAt ? v.visitedAt.toISOString() : undefined,
        subjective: v.subjective,
        objective: v.objective,
        assessment: v.assessment,
        plan: v.plan,
        followUpOn: v.followUpOn ? v.followUpOn.format('YYYY-MM-DD') : null,
        shareWithFamily: !!v.shareWithFamily,
      };
      if (editing === 'new') {
        await clinicalApi.createDoctorVisit(residentId, payload);
      } else if (editing) {
        await clinicalApi.updateDoctorVisit(residentId, editing.id, payload);
      }
      message.success('Đã lưu phiếu khám bác sĩ.');
      setEditing(null);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      {active && isDoctor && (
        <Button
          type="primary"
          icon={<PlusOutlined />}
          style={{ marginBottom: 12 }}
          onClick={() => {
            form.resetFields();
            form.setFieldsValue({ visitedAt: dayjs(), shareWithFamily: false });
            setEditing('new');
          }}
        >
          Ghi phiếu khám (SOAP)
        </Button>
      )}

      <Table
        rowKey="id"
        size="small"
        dataSource={rows}
        pagination={{ pageSize: 8 }}
        columns={[
          { title: 'Ngày khám', dataIndex: 'visitedAt', render: dateTime, width: 150 },
          { title: 'Bác sĩ', dataIndex: 'doctorName', width: 150 },
          { title: 'Nhận định (A)', dataIndex: 'assessment' },
          { title: 'Kế hoạch (P)', dataIndex: 'plan', ellipsis: true },
          { title: 'Tái khám', dataIndex: 'followUpOn', render: (d?: string) => (d ? dateOnly(d) : '—'), width: 110 },
          { title: 'Gia đình', dataIndex: 'shareWithFamily', render: (s: boolean) => (s ? <Tag color="blue">Chia sẻ</Tag> : 'Nội bộ'), width: 90 },
          {
            title: '',
            key: 'a',
            render: (_, v) =>
              canEditVisit(v) && (
                <Button
                  size="small"
                  onClick={() => {
                    form.setFieldsValue({
                      visitedAt: dayjs(v.visitedAt),
                      subjective: v.subjective,
                      objective: v.objective,
                      assessment: v.assessment,
                      plan: v.plan,
                      followUpOn: v.followUpOn ? dayjs(v.followUpOn) : null,
                      shareWithFamily: v.shareWithFamily,
                    });
                    setEditing(v);
                  }}
                >
                  Sửa
                </Button>
              ),
          },
        ]}
      />

      <Modal
        open={editing !== null}
        title={editing === 'new' ? 'Ghi phiếu khám bác sĩ (SOAP)' : 'Cập nhật phiếu khám (trong 24 giờ)'}
        onOk={save}
        onCancel={() => setEditing(null)}
        confirmLoading={submitting}
        width={640}
        okText="Lưu phiếu khám"
        destroyOnHidden
      >
        <Form form={form} layout="vertical">
          <Space wrap>
            <Form.Item name="visitedAt" label="Thời điểm khám">
              <DatePicker showTime format="DD/MM/YYYY HH:mm" />
            </Form.Item>
            <Form.Item name="followUpOn" label="Ngày hẹn tái khám">
              <DatePicker format="DD/MM/YYYY" />
            </Form.Item>
          </Space>
          <Form.Item name="subjective" label="S — Triệu chứng cơ năng / lời kể">
            <Input.TextArea rows={2} maxLength={2000} />
          </Form.Item>
          <Form.Item name="objective" label="O — Khám thực thể / cận lâm sàng">
            <Input.TextArea rows={2} maxLength={2000} />
          </Form.Item>
          <Form.Item name="assessment" label="A — Nhận định / đánh giá" rules={[{ required: true, message: 'Nhập nhận định' }]}>
            <Input.TextArea rows={2} maxLength={2000} />
          </Form.Item>
          <Form.Item name="plan" label="P — Kế hoạch điều trị / theo dõi">
            <Input.TextArea rows={2} maxLength={2000} />
          </Form.Item>
          <Form.Item name="shareWithFamily" valuePropName="checked">
            <Checkbox>Chia sẻ tóm tắt phiếu khám này với gia đình</Checkbox>
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}

// ======================= S3-H5 (UC-68): Ngưỡng sinh hiệu riêng =======================
export function VitalThresholdCard({ residentId, canEdit }: { residentId: number; canEdit: boolean }) {
  const { message } = App.useApp();
  const { user } = useAuth();
  const allowSet = canEdit && hasRole(user?.role, PRESCRIBERS);

  const [threshold, setThreshold] = useState<VitalThreshold>();
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form] = Form.useForm<SaveThresholdForm>();

  const load = useCallback(() => {
    clinicalApi.getThreshold(residentId).then(setThreshold).catch(() => undefined);
  }, [residentId]);

  useEffect(() => {
    void load();
  }, [load]);

  const save = async () => {
    const v = await form.validateFields();
    setSaving(true);
    try {
      const updated = await clinicalApi.setThreshold(residentId, v);
      setThreshold(updated);
      message.success('Đã cập nhật ngưỡng cảnh báo sinh hiệu riêng.');
      setOpen(false);
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  if (!threshold) return null;

  return (
    <>
      <Card
        size="small"
        style={{ marginBottom: 12 }}
        title={
          <Space>
            <span>Ngưỡng cảnh báo sinh hiệu</span>
            {threshold.isCustom ? <Tag color="blue">Ngưỡng riêng</Tag> : <Tag>Mặc định cơ sở</Tag>}
          </Space>
        }
        extra={
          allowSet && (
            <Button
              size="small"
              icon={<SettingOutlined />}
              onClick={() => {
                form.setFieldsValue({ ...threshold, note: threshold.note ?? undefined });
                setOpen(true);
              }}
            >
              Đặt ngưỡng riêng
            </Button>
          )
        }
      >
        <Descriptions size="small" column={{ xs: 1, sm: 2, md: 4 }}>
          <Descriptions.Item label="Huyết áp tâm thu">
            {threshold.systolicLow} – {threshold.systolicHigh} mmHg
          </Descriptions.Item>
          <Descriptions.Item label="Nhịp tim">
            {threshold.heartRateLow} – {threshold.heartRateHigh} l/p
          </Descriptions.Item>
          <Descriptions.Item label="Nhiệt độ ≥">{threshold.temperatureHigh}°C</Descriptions.Item>
          <Descriptions.Item label="SpO2 &lt;">{threshold.spO2Low}%</Descriptions.Item>
          {threshold.note && (
            <Descriptions.Item label="Ghi chú" span={4}>
              {threshold.note} {threshold.setByName ? `(${threshold.setByName})` : ''}
            </Descriptions.Item>
          )}
        </Descriptions>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          Công cụ sàng lọc, không thay thế chẩn đoán.
        </Typography.Text>
      </Card>

      <Modal
        open={open}
        title="Đặt ngưỡng cảnh báo sinh hiệu riêng"
        onOk={save}
        onCancel={() => setOpen(false)}
        confirmLoading={saving}
        okText="Lưu ngưỡng"
        destroyOnHidden
      >
        <Form form={form} layout="vertical">
          <Space wrap>
            <Form.Item name="systolicLow" label="HA tâm thu dưới" rules={[{ required: true }]}>
              <InputNumber min={50} max={160} />
            </Form.Item>
            <Form.Item name="systolicHigh" label="HA tâm thu trên" rules={[{ required: true }]}>
              <InputNumber min={90} max={260} />
            </Form.Item>
            <Form.Item name="heartRateLow" label="Mạch dưới" rules={[{ required: true }]}>
              <InputNumber min={30} max={100} />
            </Form.Item>
            <Form.Item name="heartRateHigh" label="Mạch trên" rules={[{ required: true }]}>
              <InputNumber min={60} max={220} />
            </Form.Item>
            <Form.Item name="temperatureHigh" label="Nhiệt độ cảnh báo (°C)" rules={[{ required: true }]}>
              <InputNumber min={35} max={42} step={0.1} />
            </Form.Item>
            <Form.Item name="spO2Low" label="SpO2 dưới (%)" rules={[{ required: true }]}>
              <InputNumber min={70} max={99} step={0.5} />
            </Form.Item>
          </Space>
          <Form.Item name="note" label="Lý do / ghi chú chuyên môn">
            <Input.TextArea rows={2} maxLength={500} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}
