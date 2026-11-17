import { useCallback, useEffect, useState } from 'react';
import { App, Button, Card, Col, Form, Input, Rate, Row, Select, Space, Table, Typography } from 'antd';
import { extractErrorMessage } from '../../api/client';
import { familyCareApi, type CreateFeedbackForm } from '../../api/familyCare';
import { portalApi } from '../../api/portal';
import { PageTitle, StatusTag } from '../../components/Common';
import { dateTime } from '../../utils/format';
import type { FamilyResident } from '../../types';
import type { Feedback, FeedbackCategory } from '../../types/care';

const CATEGORY_OPTIONS: { value: FeedbackCategory; label: string }[] = [
  { value: 'Care', label: 'Chăm sóc' },
  { value: 'Food', label: 'Ăn uống' },
  { value: 'Facility', label: 'Cơ sở vật chất' },
  { value: 'Billing', label: 'Hóa đơn' },
  { value: 'Staff', label: 'Nhân viên' },
  { value: 'Other', label: 'Khác' },
];

/** S3-H6 (UC-109): gia đình gửi phản hồi / khiếu nại và theo dõi trả lời. */
export default function FamilyFeedback() {
  const { message } = App.useApp();
  const [residents, setResidents] = useState<FamilyResident[]>([]);
  const [rows, setRows] = useState<Feedback[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [form] = Form.useForm<CreateFeedbackForm>();

  const load = useCallback(() => {
    familyCareApi.myFeedbacks().then(setRows).catch(() => undefined);
  }, []);

  useEffect(() => {
    portalApi.familyResidents().then(setResidents).catch(() => undefined);
    load();
  }, [load]);

  const submit = async () => {
    const v = await form.validateFields();
    setSubmitting(true);
    try {
      await familyCareApi.createFeedback(v);
      message.success('Đã gửi phản hồi tới ban quản lý.');
      form.resetFields();
      form.setFieldValue('category', 'Care');
      load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <PageTitle title="Góp ý & phản hồi" />
      <Row gutter={[16, 16]}>
        <Col xs={24} lg={9}>
          <Card title="Gửi góp ý / khiếu nại">
            <Form form={form} layout="vertical" initialValues={{ category: 'Care' }}>
              <Form.Item name="subject" label="Chủ đề" rules={[{ required: true, message: 'Nhập chủ đề' }]}>
                <Input maxLength={200} placeholder="Ví dụ: Thực đơn bữa trưa, đèn hành lang..." />
              </Form.Item>
              <Form.Item name="category" label="Nhóm vấn đề" rules={[{ required: true }]}>
                <Select options={CATEGORY_OPTIONS} />
              </Form.Item>
              <Form.Item name="residentId" label="Người thân liên quan (tùy chọn)">
                <Select
                  allowClear
                  placeholder="Chọn nếu liên quan một người thân"
                  options={residents.map((r) => ({ value: r.residentId, label: `${r.code} · ${r.fullName}` }))}
                />
              </Form.Item>
              <Form.Item name="rating" label="Đánh giá mức độ hài lòng (tùy chọn)">
                <Rate />
              </Form.Item>
              <Form.Item name="content" label="Nội dung chi tiết" rules={[{ required: true, message: 'Nhập nội dung' }]}>
                <Input.TextArea rows={4} maxLength={2000} />
              </Form.Item>
              <Button type="primary" block loading={submitting} onClick={submit}>
                Gửi phản hồi
              </Button>
            </Form>
          </Card>
        </Col>
        <Col xs={24} lg={15}>
          <Card title="Phản hồi đã gửi">
            <Table
              rowKey="id"
              size="small"
              dataSource={rows}
              pagination={{ pageSize: 8 }}
              scroll={{ x: 520 }}
              columns={[
                { title: 'Ngày gửi', dataIndex: 'createdAt', render: dateTime, width: 145 },
                {
                  title: 'Chủ đề',
                  render: (_, f) => (
                    <Space orientation="vertical" size={2}>
                      <b>{f.subject}</b>
                      <Space size={4}>
                        <StatusTag kind="feedbackCategory" value={f.category} />
                        {f.rating != null && <Rate disabled value={f.rating} style={{ fontSize: 12 }} />}
                      </Space>
                      <Typography.Text type="secondary">{f.content}</Typography.Text>
                      {f.response && (
                        <Typography.Text type="success">
                          <b>Trả lời ({f.respondedByName}):</b> {f.response}
                        </Typography.Text>
                      )}
                    </Space>
                  ),
                },
                {
                  title: 'Trạng thái',
                  dataIndex: 'status',
                  width: 110,
                  render: (v: string) => <StatusTag kind="feedbackStatus" value={v} />,
                },
              ]}
            />
          </Card>
        </Col>
      </Row>
    </>
  );
}
