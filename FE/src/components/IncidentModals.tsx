import { useEffect, useState } from 'react';
import { App, Button, Checkbox, DatePicker, Descriptions, Drawer, Form, Input, Modal, Select, Space, Tag } from 'antd';
import { incidentsApi, type ReportIncidentForm, type UpdateIncidentForm } from '../api/incidents';
import { extractErrorMessage } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { CARE_PLANNERS, hasRole } from '../auth/roles';
import { ResidentSelect, StatusTag } from './Common';
import { dateTime, dayjs } from '../utils/format';
import type { Incident, IncidentSeverity, IncidentType } from '../types/care';

export const INCIDENT_TYPE_OPTIONS: { value: IncidentType; label: string }[] = [
  { value: 'Fall', label: 'Té ngã' },
  { value: 'Injury', label: 'Chấn thương' },
  { value: 'Behavior', label: 'Hành vi' },
  { value: 'Other', label: 'Khác' },
];

export const INCIDENT_SEVERITY_OPTIONS: { value: IncidentSeverity; label: string }[] = [
  { value: 'Low', label: 'Thấp' },
  { value: 'Medium', label: 'Trung bình' },
  { value: 'High', label: 'Cao' },
];

interface ReportValues {
  residentId: number;
  type: IncidentType;
  severity: IncidentSeverity;
  occurredAt: dayjs.Dayjs;
  location?: string;
  description: string;
  immediateAction?: string;
}

export function ReportIncidentModal({
  open,
  residentId,
  onClose,
  onSaved,
}: {
  open: boolean;
  residentId?: number;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { message } = App.useApp();
  const [form] = Form.useForm<ReportValues>();
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (open) {
      form.resetFields();
      form.setFieldsValue({
        residentId,
        type: 'Fall',
        severity: 'Medium',
        occurredAt: dayjs(),
      });
    }
  }, [open, residentId, form]);

  const handleOk = async () => {
    const v = await form.validateFields();
    setSubmitting(true);
    try {
      const payload: ReportIncidentForm = {
        residentId: v.residentId,
        type: v.type,
        severity: v.severity,
        occurredAt: v.occurredAt.toISOString(),
        location: v.location,
        description: v.description,
        immediateAction: v.immediateAction,
      };
      await incidentsApi.report(payload);
      message.success(
        v.severity === 'High'
          ? 'Đã báo sự cố mức CAO và gửi cảnh báo ngay cho Điều dưỡng, Bác sĩ, Admin.'
          : 'Đã ghi nhận báo cáo sự cố.',
      );
      onSaved();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal
      open={open}
      title="Báo cáo sự cố"
      onOk={handleOk}
      onCancel={onClose}
      confirmLoading={submitting}
      okText="Gửi báo cáo"
      destroyOnHidden
    >
      <Form form={form} layout="vertical">
        <Form.Item name="residentId" label="Cư dân" rules={[{ required: true, message: 'Chọn cư dân' }]}>
          <ResidentSelect disabled={residentId !== undefined} />
        </Form.Item>
        <Space style={{ display: 'flex' }} align="start" wrap>
          <Form.Item name="type" label="Loại sự cố" rules={[{ required: true }]} style={{ minWidth: 160 }}>
            <Select options={INCIDENT_TYPE_OPTIONS} />
          </Form.Item>
          <Form.Item name="severity" label="Mức độ" rules={[{ required: true }]} style={{ minWidth: 160 }}>
            <Select options={INCIDENT_SEVERITY_OPTIONS} />
          </Form.Item>
        </Space>
        <Form.Item name="occurredAt" label="Thời điểm xảy ra" rules={[{ required: true, message: 'Chọn thời điểm' }]}>
          <DatePicker showTime format="DD/MM/YYYY HH:mm" style={{ width: '100%' }} allowClear={false} />
        </Form.Item>
        <Form.Item name="location" label="Vị trí">
          <Input maxLength={100} placeholder="Ví dụ: Phòng 101, hành lang tầng 2..." />
        </Form.Item>
        <Form.Item name="description" label="Mô tả chi tiết" rules={[{ required: true, message: 'Nhập mô tả sự cố' }]}>
          <Input.TextArea rows={3} maxLength={2000} />
        </Form.Item>
        <Form.Item name="immediateAction" label="Xử trí ban đầu">
          <Input.TextArea rows={2} maxLength={2000} placeholder="Đỡ dậy, kiểm tra sinh hiệu, chườm lạnh..." />
        </Form.Item>
      </Form>
    </Modal>
  );
}

interface UpdateValues {
  status: 'Investigating' | 'Closed';
  resolution?: string;
  shareWithFamily: boolean;
  notifyFamily: boolean;
}

export function IncidentDetailDrawer({
  incident,
  onClose,
  onUpdated,
}: {
  incident: Incident | null;
  onClose: () => void;
  onUpdated: (next: Incident) => void;
}) {
  const { message } = App.useApp();
  const { user } = useAuth();
  const [updating, setUpdating] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [form] = Form.useForm<UpdateValues>();

  const canHandle = hasRole(user?.role, [...CARE_PLANNERS, 'Doctor']) && incident?.status !== 'Closed';
  const statusWatch = Form.useWatch('status', form);
  const shareWatch = Form.useWatch('shareWithFamily', form);

  const openUpdate = () => {
    if (!incident) return;
    form.setFieldsValue({
      status: incident.status === 'Open' ? 'Investigating' : 'Closed',
      resolution: incident.resolution ?? '',
      shareWithFamily: incident.shareWithFamily,
      notifyFamily: false,
    });
    setUpdating(true);
  };

  const handleUpdate = async () => {
    if (!incident) return;
    const v = await form.validateFields();
    setSubmitting(true);
    try {
      const payload: UpdateIncidentForm = {
        status: v.status,
        resolution: v.resolution,
        shareWithFamily: v.shareWithFamily,
        notifyFamily: v.status === 'Closed' && v.shareWithFamily ? !!v.notifyFamily : false,
        rowVersion: incident.rowVersion,
      };
      const updated = await incidentsApi.update(incident.id, payload);
      message.success('Đã cập nhật xử lý sự cố.');
      setUpdating(false);
      onUpdated(updated);
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <Drawer
        open={incident !== null}
        title={incident ? `Sự cố #${incident.id} · ${incident.residentName}` : 'Chi tiết sự cố'}
        onClose={onClose}
        size={520}
        extra={
          canHandle && (
            <Button type="primary" onClick={openUpdate}>
              Cập nhật xử lý
            </Button>
          )
        }
      >
        {incident && (
          <Descriptions column={1} bordered size="small">
            <Descriptions.Item label="Cư dân">
              {incident.residentCode} · {incident.residentName}
            </Descriptions.Item>
            <Descriptions.Item label="Loại">
              <StatusTag kind="incidentType" value={incident.type} />
            </Descriptions.Item>
            <Descriptions.Item label="Mức độ">
              <StatusTag kind="incidentSeverity" value={incident.severity} />
            </Descriptions.Item>
            <Descriptions.Item label="Trạng thái">
              <StatusTag kind="incident" value={incident.status} />
            </Descriptions.Item>
            <Descriptions.Item label="Thời điểm xảy ra">{dateTime(incident.occurredAt)}</Descriptions.Item>
            <Descriptions.Item label="Vị trí">{incident.location || '—'}</Descriptions.Item>
            <Descriptions.Item label="Mô tả">{incident.description}</Descriptions.Item>
            <Descriptions.Item label="Xử trí ban đầu">{incident.immediateAction || '—'}</Descriptions.Item>
            <Descriptions.Item label="Người báo">{incident.reportedByName}</Descriptions.Item>
            <Descriptions.Item label="Kết luận xử lý">{incident.resolution || '—'}</Descriptions.Item>
            <Descriptions.Item label="Người đóng">{incident.closedByName || '—'}</Descriptions.Item>
            <Descriptions.Item label="Thời điểm đóng">
              {incident.closedAt ? dateTime(incident.closedAt) : '—'}
            </Descriptions.Item>
            <Descriptions.Item label="Gia đình">
              {incident.shareWithFamily ? <Tag color="blue">Đã chia sẻ</Tag> : 'Chưa chia sẻ'}
              {incident.familyNotifiedAt && ` (đã báo lúc ${dateTime(incident.familyNotifiedAt)})`}
            </Descriptions.Item>
          </Descriptions>
        )}
      </Drawer>

      <Modal
        open={updating}
        title="Cập nhật xử lý sự cố"
        onOk={handleUpdate}
        onCancel={() => setUpdating(false)}
        confirmLoading={submitting}
        okText="Lưu cập nhật"
        destroyOnHidden
      >
        <Form form={form} layout="vertical">
          <Form.Item name="status" label="Trạng thái" rules={[{ required: true }]}>
            <Select
              options={[
                { value: 'Investigating', label: 'Đang xử lý (Investigating)' },
                { value: 'Closed', label: 'Đóng sự cố (Closed)' },
              ]}
            />
          </Form.Item>
          <Form.Item
            name="resolution"
            label="Kết luận / hướng xử lý"
            rules={[
              {
                required: statusWatch === 'Closed',
                message: 'Khi đóng sự cố bắt buộc phải nhập kết luận',
              },
            ]}
          >
            <Input.TextArea rows={3} maxLength={2000} />
          </Form.Item>
          <Form.Item name="shareWithFamily" valuePropName="checked">
            <Checkbox>Chia sẻ sự cố này với gia đình</Checkbox>
          </Form.Item>
          {statusWatch === 'Closed' && shareWatch && (
            <Form.Item name="notifyFamily" valuePropName="checked">
              <Checkbox>Gửi thông báo ngay cho gia đình (có quyền Xem chăm sóc)</Checkbox>
            </Form.Item>
          )}
        </Form>
      </Modal>
    </>
  );
}
