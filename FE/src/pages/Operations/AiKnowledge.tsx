import { useCallback, useEffect, useState } from 'react';
import { App, Button, Form, Input, Modal, Popconfirm, Space, Switch, Table, Tag, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { aiApi } from '../../api/ai';
import { extractErrorMessage } from '../../api/client';
import { PageTitle } from '../../components/Common';
import { dateTime } from '../../utils/format';
import type { AiKnowledgeDoc, AiKnowledgeForm } from '../../types/operations';

/**
 * AI-01 (Admin): tài liệu tri thức cho chatbot (quy trình nhập viện, giờ thăm, chính sách…).
 * Giá gói và số giường không cần ghi ở đây — chatbot lấy trực tiếp từ dữ liệu.
 */
export default function AiKnowledge() {
  const { message } = App.useApp();
  const [rows, setRows] = useState<AiKnowledgeDoc[]>([]);
  const [editing, setEditing] = useState<AiKnowledgeDoc | 'new' | null>(null);
  const [form] = Form.useForm<AiKnowledgeForm>();

  const load = useCallback(() => aiApi.knowledge().then(setRows).catch((e) => message.error(extractErrorMessage(e))), [message]);
  useEffect(() => {
    void load();
  }, [load]);

  const save = async () => {
    const values = await form.validateFields();
    try {
      if (editing === 'new') await aiApi.createKnowledge(values);
      else if (editing) await aiApi.updateKnowledge(editing.id, values);
      message.success('Đã lưu tài liệu.');
      setEditing(null);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  const remove = async (doc: AiKnowledgeDoc) => {
    try {
      await aiApi.deleteKnowledge(doc.id);
      message.success('Đã xóa tài liệu.');
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <>
      <PageTitle
        title="Tri thức cho trợ lý AI"
        extra={<Button type="primary" icon={<PlusOutlined />} onClick={() => { form.resetFields(); form.setFieldsValue({ isPublic: true, isActive: true }); setEditing('new'); }}>Thêm tài liệu</Button>}
      />
      <Typography.Paragraph type="secondary">
        Chatbot tìm tài liệu theo từ khóa (tiêu đề, thẻ, nội dung) rồi trả lời dựa trên đó. Viết ngắn gọn, mỗi tài liệu một chủ đề.
        Tài liệu "Nội bộ" chỉ trợ lý gia đình/nhân viên dùng, chatbot công khai không thấy.
      </Typography.Paragraph>
      <Table<AiKnowledgeDoc>
        rowKey="id"
        dataSource={rows}
        pagination={{ pageSize: 10, hideOnSinglePage: true }}
        expandable={{ expandedRowRender: (doc) => <Typography.Paragraph style={{ whiteSpace: 'pre-wrap', margin: 0 }}>{doc.content}</Typography.Paragraph> }}
        columns={[
          { title: 'Tiêu đề', dataIndex: 'title' },
          { title: 'Thẻ', dataIndex: 'tags', render: (v?: string) => v?.split(',').map((t) => <Tag key={t}>{t.trim()}</Tag>) },
          { title: 'Phạm vi', dataIndex: 'isPublic', render: (v: boolean) => (v ? <Tag color="green">Công khai</Tag> : <Tag color="purple">Nội bộ</Tag>) },
          { title: 'Trạng thái', dataIndex: 'isActive', render: (v: boolean) => (v ? <Tag color="blue">Đang dùng</Tag> : <Tag>Tắt</Tag>) },
          { title: 'Cập nhật', dataIndex: 'updatedAt', render: dateTime },
          {
            title: '', render: (_, doc) => (
              <Space>
                <Button size="small" onClick={() => { form.setFieldsValue(doc); setEditing(doc); }}>Sửa</Button>
                <Popconfirm title="Xóa tài liệu này?" okText="Xóa" cancelText="Không" onConfirm={() => remove(doc)}>
                  <Button size="small" danger>Xóa</Button>
                </Popconfirm>
              </Space>
            ),
          },
        ]}
      />
      <Modal open={editing !== null} width={720} title={editing === 'new' ? 'Thêm tài liệu' : 'Sửa tài liệu'} onOk={save} onCancel={() => setEditing(null)} okText="Lưu" destroyOnHidden>
        <Form form={form} layout="vertical">
          <Form.Item name="title" label="Tiêu đề" rules={[{ required: true, max: 200 }]}><Input /></Form.Item>
          <Form.Item name="content" label="Nội dung" rules={[{ required: true, min: 10, max: 20000 }]}>
            <Input.TextArea rows={8} showCount maxLength={20000} />
          </Form.Item>
          <Form.Item name="tags" label="Thẻ (từ khóa, cách nhau bằng dấu phẩy)" rules={[{ max: 200 }]}><Input placeholder="giờ thăm, quy định" /></Form.Item>
          <Space size="large">
            <Form.Item name="isPublic" label="Chatbot công khai được dùng" valuePropName="checked"><Switch /></Form.Item>
            <Form.Item name="isActive" label="Đang dùng" valuePropName="checked"><Switch /></Form.Item>
          </Space>
        </Form>
      </Modal>
    </>
  );
}
