import { useEffect, useState } from 'react';
import { Card, Col, Row, Statistic, Typography } from 'antd';
import { ClockCircleOutlined, EnvironmentOutlined, PhoneOutlined } from '@ant-design/icons';
import { portalApi } from '../../api/portal';
import { money } from '../../utils/format';
import type { PublicOverview } from '../../types';

/** UC-14..UC-16: giới thiệu cơ sở, bảng giá gói dịch vụ, số giường trống (không lộ thông tin cư dân). */
export default function PublicHome() {
  const [data, setData] = useState<PublicOverview>();
  useEffect(() => {
    portalApi.publicOverview().then(setData).catch(() => undefined);
  }, []);

  return (
    <>
      <section className="hero">
        <h1>{data?.facilityName ?? 'Viện Dưỡng Lão'}</h1>
        <Typography.Paragraph style={{ color: '#e8f5f1', fontSize: 17 }}>
          Chăm sóc tận tâm như ở nhà — theo dõi sức khỏe, dinh dưỡng và tinh thần mỗi ngày.
        </Typography.Paragraph>
      </section>
      <div className="container">
        <Row gutter={[16, 16]}>
          <Col xs={24} md={8}><Card><Statistic title="Giường còn trống" value={data?.availableBeds ?? 0} suffix={`/ ${data?.totalBeds ?? 0}`} /></Card></Col>
          <Col xs={24} md={16}>
            <Card>
              <p><EnvironmentOutlined /> {data?.address}</p>
              <p><PhoneOutlined /> Hotline: {data?.hotline}</p>
              <p style={{ marginBottom: 0 }}><ClockCircleOutlined /> Giờ thăm: {data?.visitingHours}</p>
            </Card>
          </Col>
        </Row>
        <Typography.Title level={3} style={{ marginTop: 32 }}>Gói dịch vụ</Typography.Title>
        <Row gutter={[16, 16]}>
          {data?.packages.map((p) => (
            <Col xs={24} sm={12} lg={6} key={p.id}>
              <Card title={p.name} style={{ height: '100%' }}>
                <Typography.Title level={4} style={{ color: '#2f7d6d', marginTop: 0 }}>{money(p.monthlyPrice)}<small>/tháng</small></Typography.Title>
                <Typography.Paragraph type="secondary">{p.description}</Typography.Paragraph>
              </Card>
            </Col>
          ))}
        </Row>
        <Typography.Paragraph type="secondary" style={{ marginTop: 24 }}>
          Đặt lịch tham quan và chatbot tư vấn AI sẽ có ở Sprint 3–4 (UC-17, UC-18, AI-01).
        </Typography.Paragraph>
      </div>
    </>
  );
}
