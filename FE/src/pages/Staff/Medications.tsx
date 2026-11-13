import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, App, Button, DatePicker, Form, Input, Modal, Radio, Space, Table } from 'antd';
import { careApi } from '../../api/care';
import { extractErrorMessage } from '../../api/client';
import { PageTitle, StatusTag } from '../../components/Common';
import { dayjs, hhmm, timeOf } from '../../utils/format';
import type { MedicationDose } from '../../types';

/**
 * UC-76, UC-77: bảng phát thuốc trong ngày. Chỉ ghi khi đã đến giờ; mỗi lần dùng một bản ghi,
 * không ghi đè; Missed/Refused bắt buộc lý do.
 */
export default function Medications() {
  const { message } = App.useApp();
  const [date, setDate] = useState(dayjs());
  const [rows, setRows] = useState<MedicationDose[]>([]);
  const [loading, setLoading] = useState(false);
  const [recording, setRecording] = useState<MedicationDose | null>(null);
  const [form] = Form.useForm<{ status: string; notes?: string }>();
  const status = Form.useWatch('status', form);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await careApi.today(date.format('YYYY-MM-DD')));
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [date, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const save = async () => {
    if (!recording) return;
    const v = await form.validateFields();
    try {
      await careApi.recordDose({ scheduleId: recording.scheduleId, date: date.format('YYYY-MM-DD'), status: v.status, notes: v.notes });
      message.success('Đã ghi nhận.');
      setRecording(null);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <>
      <PageTitle title="Phát thuốc" extra={<DatePicker value={date} onChange={(d) => d && setDate(d)} format="DD/MM/YYYY" allowClear={false} />} />
      <Alert type="info" showIcon style={{ marginBottom: 16 }} title="Lịch thuốc do bác sĩ nhập theo chỉ định. Nhân viên chỉ ghi nhận Đã dùng / Bỏ lỡ / Từ chối." />
      <Table
        rowKey={(r) => `${r.scheduleId}-${r.scheduledAt}`}
        loading={loading}
        dataSource={rows}
        pagination={false}
        columns={[
          { title: 'Giờ', dataIndex: 'scheduledTime', render: hhmm, width: 70 },
          { title: 'Cư dân', render: (_, d) => <Link to={`/staff/residents/${d.residentId}`}>{d.residentCode} · {d.residentName}</Link> },
          { title: 'Thuốc', render: (_, d) => <>{d.medicationName}<br /><small>{d.dose} · {d.route}{d.instructions ? ` · ${d.instructions}` : ''}</small></> },
          {
            title: 'Trạng thái', render: (_, d) => (
              <Space direction="vertical" size={0}>
                <StatusTag kind="medication" value={d.status} />
                {d.recordedAt && <small>{timeOf(d.recordedAt)} · {d.recordedByName}</small>}
                {d.notes && <small>{d.notes}</small>}
              </Space>
            ),
          },
          {
            title: '', key: 'a', render: (_, d) => d.status === 'Scheduled' && (
              <Button size="small" type="primary" disabled={!d.canRecord} title={d.canRecord ? '' : 'Chưa đến giờ dùng'}
                onClick={() => { form.resetFields(); form.setFieldsValue({ status: 'Taken' }); setRecording(d); }}>
                Ghi nhận
              </Button>
            ),
          },
        ]}
      />
      <Modal open={!!recording} title={`${recording?.medicationName} — ${recording?.residentName}`} onOk={save} onCancel={() => setRecording(null)} okText="Lưu" destroyOnHidden>
        <Form form={form} layout="vertical">
          <Form.Item name="status" label="Kết quả" rules={[{ required: true }]}>
            <Radio.Group options={[{ value: 'Taken', label: 'Đã dùng' }, { value: 'Missed', label: 'Bỏ lỡ' }, { value: 'Refused', label: 'Từ chối' }]} />
          </Form.Item>
          <Form.Item name="notes" label="Ghi chú / lý do" rules={[{ required: status !== 'Taken', message: 'Bắt buộc ghi lý do' }]}>
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}
