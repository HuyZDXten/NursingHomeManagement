import { useEffect, useState } from 'react';
import { App, Checkbox, DatePicker, Drawer, Form, Input, Modal, Select, Tag, Timeline, TimePicker } from 'antd';
import { careApi } from '../api/care';
import { extractErrorMessage } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { dateTime, dayjs } from '../utils/format';
import { statusMeta } from '../utils/status';
import type { CareRecord, CareTask } from '../types';
import { AccountSelect, ResidentSelect } from './Common';

/** UC-50: tạo & phân công việc cho Nurse/Caregiver (Admin, Nurse). */
export function CreateTaskModal({ open, residentId, onClose, onSaved }: { open: boolean; residentId?: number; onClose: () => void; onSaved: () => void }) {
  const { message } = App.useApp();
  const [form] = Form.useForm();

  useEffect(() => {
    if (open) {
      form.resetFields();
      form.setFieldsValue({ residentId, dueDate: dayjs(), dueTime: dayjs().add(1, 'hour').minute(0) });
    }
  }, [open, residentId, form]);

  const save = async () => {
    const v = await form.validateFields();
    try {
      await careApi.createTask({
        residentId: v.residentId,
        assignedToId: v.assignedToId,
        title: v.title,
        instructions: v.instructions,
        dueDate: v.dueDate.format('YYYY-MM-DD'),
        dueTime: v.dueTime.format('HH:mm:00'),
      });
      message.success('Đã giao việc và gửi thông báo cho người được phân công.');
      onSaved();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <Modal open={open} title="Giao công việc chăm sóc" onOk={save} onCancel={onClose} okText="Giao việc" destroyOnHidden>
      <Form form={form} layout="vertical">
        <Form.Item name="residentId" label="Cư dân" rules={[{ required: true }]}><ResidentSelect disabled={!!residentId} /></Form.Item>
        <Form.Item name="assignedToId" label="Giao cho" rules={[{ required: true }]}><AccountSelect roles={['Caregiver', 'Nurse']} /></Form.Item>
        <Form.Item name="title" label="Công việc" rules={[{ required: true, max: 200 }]}><Input placeholder="VD: Hỗ trợ tắm, đo sinh hiệu..." /></Form.Item>
        <Form.Item name="instructions" label="Hướng dẫn"><Input.TextArea rows={2} /></Form.Item>
        <div style={{ display: 'flex', gap: 12 }}>
          <Form.Item name="dueDate" label="Ngày" rules={[{ required: true }]} style={{ flex: 1 }}><DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="dueTime" label="Giờ" rules={[{ required: true }]} style={{ flex: 1 }}><TimePicker format="HH:mm" minuteStep={5} style={{ width: '100%' }} /></Form.Item>
        </div>
      </Form>
    </Modal>
  );
}

/** UC-53/UC-54: cập nhật trạng thái + kết quả, gửi kèm rowVersion để chống ghi đè. */
export function UpdateTaskModal({ task, onClose, onSaved }: { task: CareTask | null; onClose: () => void; onSaved: () => void }) {
  const { message } = App.useApp();
  const { user } = useAuth();
  const [form] = Form.useForm();

  useEffect(() => {
    if (task) {
      form.resetFields();
      form.setFieldsValue({ status: task.status === 'Pending' ? 'InProgress' : 'Completed', shareWithFamily: false });
    }
  }, [task, form]);

  const options = [
    { value: 'InProgress', label: 'Đang làm' },
    { value: 'Completed', label: 'Hoàn thành' },
    ...(user?.role === 'Admin' ? [{ value: 'Cancelled', label: 'Hủy (chỉ Admin)' }] : []),
  ];

  const save = async () => {
    if (!task) return;
    const v = await form.validateFields();
    try {
      await careApi.updateTask(task.id, { ...v, rowVersion: task.rowVersion });
      message.success('Đã cập nhật công việc.');
      onSaved();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <Modal open={!!task} title={`Cập nhật: ${task?.title ?? ''}`} onOk={save} onCancel={onClose} okText="Lưu" destroyOnHidden>
      <p>{task?.residentCode} — {task?.residentName} · hạn {dateTime(task?.dueAt)}</p>
      {task?.instructions && <p><i>{task.instructions}</i></p>}
      <Form form={form} layout="vertical">
        <Form.Item name="status" label="Trạng thái" rules={[{ required: true }]}><Select options={options} /></Form.Item>
        <Form.Item name="result" label="Kết quả / lý do" rules={[{ required: true, message: 'Bắt buộc ghi kết quả' }, { max: 2000 }]}><Input.TextArea rows={3} /></Form.Item>
        <Form.Item name="shareWithFamily" valuePropName="checked"><Checkbox>Chia sẻ ghi chép này với gia đình</Checkbox></Form.Item>
      </Form>
    </Modal>
  );
}

/** UC-55: lịch sử ghi chép của một công việc. */
export function TaskRecordsDrawer({ task, onClose }: { task: CareTask | null; onClose: () => void }) {
  const [records, setRecords] = useState<CareRecord[]>([]);
  useEffect(() => {
    if (task) careApi.records(task.id).then(setRecords).catch(() => setRecords([]));
  }, [task]);
  return (
    <Drawer open={!!task} onClose={onClose} title={`Lịch sử: ${task?.title ?? ''}`}>
      {records.length === 0 ? <p>Chưa có ghi chép.</p> : (
        <Timeline
          items={records.map((r) => ({
            color: statusMeta('care', r.status).color === 'success' ? 'green' : 'blue',
            children: (
              <>
                <b>{statusMeta('care', r.status).label}</b> · {dateTime(r.performedAt)} · {r.recordedByName}
                {r.shareWithFamily && <Tag color="purple" style={{ marginLeft: 6 }}>Gia đình xem được</Tag>}
                <div>{r.result}</div>
              </>
            ),
          }))}
        />
      )}
    </Drawer>
  );
}
