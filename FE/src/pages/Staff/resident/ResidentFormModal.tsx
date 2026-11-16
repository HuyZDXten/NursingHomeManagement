import { useEffect } from 'react';
import { App, DatePicker, Form, Input, Modal, Select } from 'antd';
import { extractErrorMessage } from '../../../api/client';
import { residentsApi } from '../../../api/residents';
import { dayjs } from '../../../utils/format';
import type { ResidentDetail } from '../../../types';

interface Props {
  open: boolean;
  resident?: ResidentDetail;
  onClose: () => void;
  onSaved: (resident: ResidentDetail) => void;
  /** Điền sẵn khi tạo mới, vd. "Nhập viện" từ yêu cầu tư vấn (S3-V1). */
  defaults?: { fullName?: string | null; notes?: string | null };
}

/** UC-23 / UC-24: tạo hoặc sửa hồ sơ cư dân. */
export function ResidentFormModal({ open, resident, onClose, onSaved, defaults }: Props) {
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const defaultName = defaults?.fullName ?? undefined;
  const defaultNotes = defaults?.notes ?? undefined;

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    if (resident) form.setFieldsValue({ ...resident, dateOfBirth: dayjs(resident.dateOfBirth) });
    else form.setFieldsValue({ fullName: defaultName, notes: defaultNotes });
  }, [open, resident, defaultName, defaultNotes, form]);

  const save = async () => {
    const values = await form.validateFields();
    const body = { ...values, dateOfBirth: values.dateOfBirth.format('YYYY-MM-DD') };
    try {
      const saved = resident ? await residentsApi.update(resident.id, body) : await residentsApi.create(body);
      message.success('Đã lưu hồ sơ cư dân.');
      onSaved(saved);
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <Modal open={open} title={resident ? 'Sửa hồ sơ cư dân' : 'Tiếp nhận cư dân mới'} onOk={save} onCancel={onClose} okText="Lưu" destroyOnHidden>
      <Form form={form} layout="vertical">
        <Form.Item name="code" label="Mã cư dân" rules={[{ required: true, max: 30 }]}><Input placeholder="NH-0006" /></Form.Item>
        <Form.Item name="fullName" label="Họ tên" rules={[{ required: true, max: 150 }]}><Input /></Form.Item>
        <Form.Item name="dateOfBirth" label="Ngày sinh" rules={[{ required: true }]}>
          <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} disabledDate={(d) => d.isAfter(dayjs())} />
        </Form.Item>
        <Form.Item name="gender" label="Giới tính" rules={[{ required: true }]}>
          <Select options={[{ value: 'Nam', label: 'Nam' }, { value: 'Nữ', label: 'Nữ' }, { value: 'Khác', label: 'Khác' }]} />
        </Form.Item>
        <Form.Item name="notes" label="Ghi chú (tiền sử, thói quen, lưu ý chăm sóc)"><Input.TextArea rows={3} maxLength={2000} /></Form.Item>
      </Form>
    </Modal>
  );
}
