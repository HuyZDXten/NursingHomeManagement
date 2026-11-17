import { useCallback, useEffect, useState } from 'react';
import { Alert, App, Button, Card, Col, Form, Input, Radio, Row, Select, Switch, Table, Tag } from 'antd';
import { SendOutlined } from '@ant-design/icons';
import { extractErrorMessage } from '../../api/client';
import { systemApi } from '../../api/system';
import { PageTitle } from '../../components/Common';
import { dateTime } from '../../utils/format';
import { ROLE_LABEL } from '../../utils/status';
import { ROLES } from '../../types';
import type { Announcement, AnnouncementAudience, AnnouncementForm } from '../../types/system';

const AUDIENCE_LABEL: Record<AnnouncementAudience, string> = {
  All: 'Tất cả tài khoản',
  Family: 'Tất cả gia đình',
  Staff: 'Tất cả nhân viên',
  Role: 'Một vai trò',
};

const audienceText = (a: Announcement) => (a.audience === 'Role' ? `Vai trò: ${ROLE_LABEL[a.role as keyof typeof ROLE_LABEL] ?? a.role}` : AUDIENCE_LABEL[a.audience]);

/** BL-V5, UC-119 (Admin): thông báo hàng loạt. Luôn có thông báo trong ứng dụng; email là tùy chọn và tôn trọng lựa chọn kênh nhận của từng người (UC-120). */
export default function Announcements() {
  const { message } = App.useApp();
  const [rows, setRows] = useState<Announcement[]>([]);
  const [sending, setSending] = useState(false);
  const [form] = Form.useForm<AnnouncementForm>();
  const audience = Form.useWatch('audience', form);

  const load = useCallback(() => systemApi.announcements().then(setRows).catch((e) => message.error(extractErrorMessage(e))), [message]);
  useEffect(() => {
    void load();
  }, [load]);

  const send = async (values: AnnouncementForm) => {
    setSending(true);
    try {
      const sent = await systemApi.sendAnnouncement({ ...values, role: values.audience === 'Role' ? values.role : undefined });
      message.success(`Đã gửi cho ${sent.recipientCount} người${sent.sendEmail ? `, ${sent.emailSentCount} email` : ''}.`);
      form.resetFields();
      form.setFieldsValue({ audience: 'All', sendEmail: false });
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSending(false);
    }
  };

  return (
    <>
      <PageTitle title="Thông báo hàng loạt" />
      <Row gutter={[16, 16]}>
        <Col xs={24} lg={10}>
          <Card title="Soạn thông báo">
            <Form form={form} layout="vertical" onFinish={send} initialValues={{ audience: 'All', sendEmail: false }}>
              <Form.Item name="title" label="Tiêu đề" rules={[{ required: true, whitespace: true, max: 200 }]}><Input /></Form.Item>
              <Form.Item name="body" label="Nội dung" rules={[{ required: true, whitespace: true, max: 2000 }]}><Input.TextArea rows={5} showCount maxLength={2000} /></Form.Item>
              <Form.Item name="audience" label="Gửi cho">
                <Radio.Group>
                  {(Object.keys(AUDIENCE_LABEL) as AnnouncementAudience[]).map((a) => <Radio key={a} value={a}>{AUDIENCE_LABEL[a]}</Radio>)}
                </Radio.Group>
              </Form.Item>
              {audience === 'Role' && (
                <Form.Item name="role" label="Vai trò" rules={[{ required: true, message: 'Chọn vai trò' }]}>
                  <Select options={ROLES.map((r) => ({ value: r, label: ROLE_LABEL[r] }))} placeholder="Chọn vai trò" />
                </Form.Item>
              )}
              <Form.Item name="sendEmail" label="Gửi thêm email" valuePropName="checked" extra="Chỉ gửi khi hệ thống đã cấu hình SMTP và người nhận chưa tắt kênh email thông báo chung. Tối đa 100 email mỗi lần.">
                <Switch />
              </Form.Item>
              <Alert type="info" showIcon style={{ marginBottom: 16 }} title="Mọi người nhận đều có thông báo trong ứng dụng (chuông), không tắt được." />
              <Button type="primary" htmlType="submit" icon={<SendOutlined />} loading={sending}>Gửi thông báo</Button>
            </Form>
          </Card>
        </Col>
        <Col xs={24} lg={14}>
          <Card title="Đã gửi gần đây">
            <Table<Announcement> rowKey="id" size="small" dataSource={rows} scroll={{ x: 560 }} pagination={{ pageSize: 8, hideOnSinglePage: true }}
              expandable={{ expandedRowRender: (row) => <div style={{ whiteSpace: 'pre-wrap' }}>{row.body}</div> }}
              columns={[
                { title: 'Thời điểm', dataIndex: 'createdAt', width: 140, render: (v: string) => dateTime(v) },
                { title: 'Tiêu đề', dataIndex: 'title' },
                { title: 'Gửi cho', render: (_, r) => audienceText(r) },
                { title: 'Người nhận', dataIndex: 'recipientCount', align: 'right', width: 100 },
                { title: 'Email', width: 90, align: 'right', render: (_, r) => (r.sendEmail ? r.emailSentCount : <Tag>Không</Tag>) },
              ]} />
          </Card>
        </Col>
      </Row>
    </>
  );
}
