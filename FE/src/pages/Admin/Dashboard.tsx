import { useEffect, useState } from 'react';
import { Card, Col, Row, Statistic, Typography } from 'antd';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { portalApi } from '../../api/portal';
import { PageTitle } from '../../components/Common';
import { money } from '../../utils/format';
import type { Dashboard as DashboardData } from '../../types';

/** UC-121: số liệu truy vấn SQL thật, không hardcode. */
export default function Dashboard() {
  const [data, setData] = useState<DashboardData>();
  useEffect(() => {
    portalApi.dashboard().then(setData).catch(() => undefined);
  }, []);

  const tiles: { title: string; value: number | string; suffix?: string }[] = data ? [
    { title: 'Cư dân đang lưu trú', value: data.activeResidents },
    { title: 'Giường trống / tổng', value: data.availableBeds, suffix: `/ ${data.beds}` },
    { title: 'Giường đang có người', value: data.occupiedBeds },
    { title: 'Giường bảo trì', value: data.maintenanceBeds },
    { title: 'Việc chăm sóc hôm nay', value: data.tasksToday },
    { title: 'Việc chưa xong', value: data.openTasks },
    { title: 'Hóa đơn chờ thu', value: data.unpaidInvoices },
    { title: 'Giao dịch PayOS đang chờ', value: data.pendingOnlinePayments },
  ] : [];

  return (
    <>
      <PageTitle title="Tổng quan" />
      <Row gutter={[16, 16]}>
        {tiles.map((t) => (
          <Col xs={12} md={6} key={t.title}><Card><Statistic title={t.title} value={t.value} suffix={t.suffix} /></Card></Col>
        ))}
        <Col xs={24} md={8}><Card><Statistic title="Đã phát hành" value={money(data?.billed)} /></Card></Col>
        <Col xs={24} md={8}><Card><Statistic title="Thực thu" value={money(data?.collected)} valueStyle={{ color: '#2f7d6d' }} /></Card></Col>
        <Col xs={24} md={8}><Card><Statistic title="Công nợ" value={money(data?.debt)} valueStyle={{ color: '#d46b08' }} /></Card></Col>
        <Col span={24}>
          <Card>
            <Typography.Text strong>Tiền thực thu 6 tháng gần nhất (VND)</Typography.Text>
            <div style={{ height: 280, marginTop: 12 }}>
              <ResponsiveContainer>
                <BarChart data={data?.collectedByMonth ?? []}>
                  <CartesianGrid strokeDasharray="3 3" vertical={false} />
                  <XAxis dataKey="month" />
                  <YAxis tickFormatter={(v: number) => `${(v / 1_000_000).toLocaleString('vi-VN')} tr`} width={60} />
                  <Tooltip formatter={(v) => money(Number(v))} />
                  <Bar dataKey="amount" name="Thực thu" fill="#2f7d6d" radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </Card>
        </Col>
      </Row>
    </>
  );
}
