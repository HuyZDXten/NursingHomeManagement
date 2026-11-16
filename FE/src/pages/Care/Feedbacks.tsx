import { useCallback, useEffect, useState } from 'react';
import { App, Button, Descriptions, Drawer, Form, Input, Rate, Select, Space, Table } from 'antd';
import { extractErrorMessage } from '../../api/client';
import { familyCareApi, type RespondFeedbackForm } from '../../api/familyCare';
import { STAFF } from '../../auth/roles';
import { AccountSelect, PageTitle, StatusTag } from '../../components/Common';
import { dateTime } from '../../utils/format';
import type { Feedback, FeedbackCategory, FeedbackStatus } from '../../types/care';

const STATUS_OPTIONS: { value: FeedbackStatus; label: string }[] = [
  { value: 'New', label: 'Mới nhận' },
  { value: 'InProgress', label: 'Đang xử lý' },
  { value: 'Resolved', label: 'Đã trả lời' },
];

const CATEGORY_OPTIONS: { value: FeedbackCategory; label: string }[] = [
  { value: 'Care', label: 'Chăm sóc' },
  { value: 'Food', label: 'Ăn uống' },
  { value: 'Facility', label: 'Cơ sở vật chất' },
  { value: 'Billing', label: 'Hóa đơn' },
  { value: 'Staff', label: 'Nhân viên' },
  { value: 'Other', label: 'Khác' },
];

/** S3-H6 (UC-110): Admin tiếp nhận, giao người xử lý và trả lời phản hồi gia đình. */
export default function Feedbacks() {
  const { message } = App.useApp();
  const [status, setStatus] = useState<FeedbackStatus>();
  const [category, setCategory] = useState<FeedbackCategory>();
  const [rows, setRows] = useState<Feedback[]>([]);
  const [loading, setLoading] = useState(false);
  const [selected, setSelected] = useState<Feedback | null>(null);
  const [saving, setSaving] = useState(false);
  const [form] = Form.useForm<RespondFeedbackForm>();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await familyCareApi.allFeedbacks(status, category));
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [status, category, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const openDrawer = (f: Feedback) => {
    setSelected(f);
    form.setFieldsValue({
      status: f.status === 'New' ? 'Resolved' : f.status,
      assignedToId: f.assignedToId ?? undefined,
      response: f.response ?? '',
    });
  };

  const submitRespond = async () => {
    if (!selected) return;
    const v = await form.validateFields();
    setSaving(true);
    try {
      const updated = await familyCareApi.respondFeedback(selected.id, v);
      message.success('Đã cập nhật phản hồi và thông báo cho gia đình.');
      setSelected(updated);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <PageTitle title="Phản hồi từ gia đình" />
      <Space style={{ marginBottom: 16 }} wrap>
        <Select allowClear placeholder="Trạng thái" style={{ width: 150 }} value={status} onChange={setStatus} options={STATUS_OPTIONS} />
        <Select allowClear placeholder="Nhóm vấn đề" style={{ width: 160 }} value={category} onChange={setCategory} options={CATEGORY_OPTIONS} />
      </Space>

      <Table
        rowKey="id"
        loading={loading}
        dataSource={rows}
        pagination={{ pageSize: 15 }}
        onRow={(r) => ({ onClick: () => openDrawer(r), style: { cursor: 'pointer' } })}
        columns={[
          { title: 'Ngày gửi', dataIndex: 'createdAt', render: dateTime, width: 145 },
          { title: 'Gia đình', dataIndex: 'familyName', width: 150 },
          { title: 'Cư dân', dataIndex: 'residentName', render: (v?: string) => v || '—', width: 150 },
          { title: 'Nhóm', dataIndex: 'category', render: (v: string) => <StatusTag kind="feedbackCategory" value={v} />, width: 130 },
          { title: 'Chủ đề', dataIndex: 'subject' },
          { title: 'Đánh giá', dataIndex: 'rating', render: (r?: number) => (r ? <Rate disabled value={r} style={{ fontSize: 12 }} /> : '—'), width: 120 },
          { title: 'Trạng thái', dataIndex: 'status', render: (v: string) => <StatusTag kind="feedbackStatus" value={v} />, width: 110 },
          {
            title: '',
            key: 'a',
            render: (_, r) => (
              <Button size="small" onClick={(e) => { e.stopPropagation(); openDrawer(r); }}>
                Xử lý
              </Button>
            ),
          },
        ]}
      />

      <Drawer
        open={selected !== null}
        title={selected ? `Phản hồi #${selected.id}: ${selected.subject}` : 'Chi tiết phản hồi'}
        onClose={() => setSelected(null)}
        width={520}
      >
        {selected && (
          <>
            <Descriptions column={1} bordered size="small" style={{ marginBottom: 16 }}>
              <Descriptions.Item label="Người gửi">{selected.familyName}</Descriptions.Item>
              <Descriptions.Item label="Cư dân">{selected.residentName || '—'}</Descriptions.Item>
              <Descriptions.Item label="Nhóm"><StatusTag kind="feedbackCategory" value={selected.category} /></Descriptions.Item>
              <Descriptions.Item label="Nội dung">{selected.content}</Descriptions.Item>
              <Descriptions.Item label="Trạng thái"><StatusTag kind="feedbackStatus" value={selected.status} /></Descriptions.Item>
            </Descriptions>

            <Form form={form} layout="vertical">
              <Form.Item name="status" label="Trạng thái xử lý" rules={[{ required: true }]}>
                <Select
                  options={[
                    { value: 'InProgress', label: 'Đang xử lý' },
                    { value: 'Resolved', label: 'Đã trả lời / giải quyết' },
                  ]}
                />
              </Form.Item>
              <Form.Item name="assignedToId" label="Giao nhân viên phụ trách (tùy chọn)">
                <AccountSelect roles={STAFF} />
              </Form.Item>
              <Form.Item name="response" label="Nội dung trả lời gửi gia đình">
                <Input.TextArea rows={4} maxLength={2000} />
              </Form.Item>
              <Button type="primary" block loading={saving} onClick={submitRespond}>
                Lưu &amp; gửi thông báo cho gia đình
              </Button>
            </Form>
          </>
        )}
      </Drawer>
    </>
  );
}
