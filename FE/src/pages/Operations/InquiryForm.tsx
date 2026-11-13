import { useState } from 'react';
import { Link } from 'react-router-dom';
import { App, Button, Card, Col, Form, Input, InputNumber, Result, Row, Select } from 'antd';
import { CalendarOutlined } from '@ant-design/icons';
import { admissionsApi } from '../../api/admissions';
import { extractErrorMessage } from '../../api/client';
import type { ServicePackage } from '../../types';
import type { InquiryForm as InquiryFormValues } from '../../types/operations';

/** Số điện thoại VN: 9–15 chữ số, cho phép khoảng trắng/dấu chấm/gạch (API chuẩn hóa lại). */
export const PHONE_RULE = { pattern: /^\+?[\d\s.\-()]{9,20}$/, message: 'Số điện thoại không hợp lệ' };

/** UC-17: form "Nhận tư vấn" ở trang chủ (không cần đăng nhập). */
export function InquiryForm({ packages }: { packages: ServicePackage[] }) {
  const { message } = App.useApp();
  const [form] = Form.useForm<InquiryFormValues>();
  const [loading, setLoading] = useState(false);
  const [sent, setSent] = useState<string>();

  const submit = async (values: InquiryFormValues) => {
    setLoading(true);
    try {
      const result = await admissionsApi.createInquiry(values);
      setSent(result.message);
      form.resetFields();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  };

  if (sent) {
    return (
      <Card>
        <Result status="success" title="Cảm ơn bạn!" subTitle={sent}
          extra={<Button onClick={() => setSent(undefined)}>Gửi yêu cầu khác</Button>} />
      </Card>
    );
  }

  return (
    <Card title="Nhận tư vấn miễn phí" extra={<Link to="/dat-lich-tham-quan"><CalendarOutlined /> Đặt lịch tham quan</Link>}>
      <Form form={form} layout="vertical" onFinish={submit} requiredMark="optional">
        <Row gutter={16}>
          <Col xs={24} md={12}>
            <Form.Item name="fullName" label="Họ tên người liên hệ" rules={[{ required: true, message: 'Nhập họ tên' }, { max: 150 }]}>
              <Input autoComplete="name" />
            </Form.Item>
          </Col>
          <Col xs={24} md={12}>
            <Form.Item name="phone" label="Số điện thoại" rules={[{ required: true, message: 'Nhập số điện thoại' }, PHONE_RULE]}>
              <Input autoComplete="tel" inputMode="tel" />
            </Form.Item>
          </Col>
          <Col xs={24} md={12}>
            <Form.Item name="email" label="Email" rules={[{ type: 'email', message: 'Email không hợp lệ' }, { max: 256 }]}>
              <Input autoComplete="email" />
            </Form.Item>
          </Col>
          <Col xs={24} md={12}>
            <Form.Item name="interestedPackageId" label="Gói quan tâm">
              <Select allowClear placeholder="Chưa chọn" options={packages.map((p) => ({ value: p.id, label: p.name }))} />
            </Form.Item>
          </Col>
          <Col xs={24} md={12}>
            <Form.Item name="elderName" label="Họ tên người cao tuổi" rules={[{ max: 150 }]}><Input /></Form.Item>
          </Col>
          <Col xs={24} md={12}>
            <Form.Item name="elderAge" label="Tuổi">
              <InputNumber<number> min={40} max={120} style={{ width: '100%' }} />
            </Form.Item>
          </Col>
          <Col span={24}>
            <Form.Item name="careNeeds" label="Nhu cầu chăm sóc" rules={[{ max: 1000 }]}>
              <Input.TextArea rows={3} maxLength={1000} showCount placeholder="Tình trạng sức khỏe, khả năng tự sinh hoạt, mong muốn của gia đình..." />
            </Form.Item>
          </Col>
        </Row>
        <Button type="primary" htmlType="submit" loading={loading}>Gửi yêu cầu</Button>
      </Form>
    </Card>
  );
}
