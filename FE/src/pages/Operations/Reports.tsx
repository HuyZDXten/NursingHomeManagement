import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Alert, App, Button, Card, Col, DatePicker, Empty, Row, Segmented, Select, Space, Statistic, Table, Tabs, Tag, Typography,
} from 'antd';
import { DownloadOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import {
  Bar, BarChart, CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts';
import { extractErrorMessage } from '../../api/client';
import { aiApi } from '../../api/ai';
import { reportsApi } from '../../api/reports';
import { useAuth } from '../../auth/AuthContext';
import { BILLING_STAFF, FINANCE_STAFF, PRESCRIBERS, hasRole } from '../../auth/roles';
import { ChatWidget } from '../../components/ChatWidget';
import { PageTitle, StatusTag } from '../../components/Common';
import { dateOnly, dayjs, money, monthOf, todayVn } from '../../utils/format';
import type { AdherenceReport, OccupancyReport, RevenueReport } from '../../types/operations';

// Màu theo skill dataviz (đã chạy validate_palette.js, nền sáng):
// - 2 nguồn thu: categorical slot 1–2 (xanh / cam) — đạt mọi kiểm tra CVD, tương phản.
// - Liều thuốc: bảng màu TRẠNG THÁI good / warning / critical — luôn kèm nhãn (legend + bảng), không dùng màu một mình.
const SERIES_1 = '#2a78d6';
const SERIES_2 = '#eb6834';
const STATUS_GOOD = '#0ca30c';
const STATUS_WARNING = '#fab219';
const STATUS_CRITICAL = '#d03b3b';
const SURFACE = '#ffffff'; // nền Card: khe 2px giữa các đoạn cột chồng
const GRID = '#f0f0f0';
const AXIS = { fontSize: 12, fill: '#52514e' };

const millions = (v: number) => `${(v / 1_000_000).toLocaleString('vi-VN')} tr`;
const yearOptions = () => {
  const current = Number(todayVn().slice(0, 4));
  return Array.from({ length: 5 }, (_, i) => ({ value: current - i, label: `Năm ${current - i}` }));
};

/** UC-122 công suất giường, UC-123 doanh thu & công nợ, UC-124 tuân thủ thuốc. Mỗi tab chỉ hiện với role được xem. */
export default function Reports() {
  const { user } = useAuth();
  const tabs = [
    hasRole(user?.role, FINANCE_STAFF) && { key: 'revenue', label: 'Doanh thu & công nợ', children: <RevenueTab /> },
    hasRole(user?.role, BILLING_STAFF) && { key: 'occupancy', label: 'Công suất giường', children: <OccupancyTab /> },
    hasRole(user?.role, PRESCRIBERS) && { key: 'adherence', label: 'Tuân thủ thuốc', children: <AdherenceTab /> },
  ].filter((tab) => !!tab);

  return (
    <>
      <PageTitle title="Báo cáo" />
      {tabs.length ? <Tabs items={tabs} /> : <Empty description="Bạn chưa có báo cáo nào được xem." />}
      {/* S4-V2 (AI-12): hỏi đáp số liệu — Admin, Kế toán; số liệu lấy từ báo cáo có sẵn, không sinh SQL */}
      {hasRole(user?.role, FINANCE_STAFF) && (
        <ChatWidget title="Hỏi đáp số liệu" greeting="Hỏi tôi về doanh thu, công nợ, công suất giường… Ví dụ: Tháng này thu được bao nhiêu?"
          send={aiApi.reportChat} loadHistory={aiApi.reportHistory} disclaimer="Số liệu lấy từ báo cáo của hệ thống; kiểm tra lại ở các tab trước khi dùng chính thức." />
      )}
    </>
  );
}

function ExportButton({ onExport }: { onExport: () => Promise<void> }) {
  const { message } = App.useApp();
  const [loading, setLoading] = useState(false);
  const run = async () => {
    setLoading(true);
    try {
      await onExport();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  };
  return <Button icon={<DownloadOutlined />} loading={loading} onClick={() => void run()}>Xuất Excel</Button>;
}

type View = 'chart' | 'table';
const ViewToggle = ({ value, onChange }: { value: View; onChange: (v: View) => void }) => (
  <Segmented<View> size="small" value={value} onChange={onChange} options={[{ value: 'chart', label: 'Biểu đồ' }, { value: 'table', label: 'Bảng' }]} />
);

function RevenueTab() {
  const { message } = App.useApp();
  const [year, setYear] = useState(() => Number(todayVn().slice(0, 4)));
  const [data, setData] = useState<RevenueReport>();
  const [view, setView] = useState<View>('chart');

  useEffect(() => {
    reportsApi.revenue(year).then(setData).catch((e) => message.error(extractErrorMessage(e)));
  }, [year, message]);

  const months = (data?.months ?? []).map((m) => ({ ...m, label: `T${m.month}` }));

  return (
    <>
      <Space style={{ marginBottom: 16 }} wrap>
        <Select value={year} onChange={setYear} options={yearOptions()} style={{ width: 130 }} />
        <ExportButton onExport={() => reportsApi.exportRevenue(year)} />
      </Space>
      <Row gutter={[16, 16]}>
        <Col xs={24} md={8}><Card><Statistic title={`Thực thu năm ${year}`} value={money(data?.totalCollected)} valueStyle={{ color: '#2f7d6d' }} /></Card></Col>
        <Col xs={24} md={8}><Card><Statistic title="Công nợ hiện tại" value={money(data?.totalDebt)} valueStyle={{ color: '#d46b08' }} /></Card></Col>
        <Col xs={24} md={8}><Card><Statistic title="Hóa đơn còn nợ" value={data?.debts.length ?? 0} /></Card></Col>
        <Col span={24}>
          <Card title="Thực thu theo tháng (VND)" extra={<ViewToggle value={view} onChange={setView} />}>
            {view === 'chart' ? (
              <div style={{ height: 300 }}>
                <ResponsiveContainer>
                  <BarChart data={months} maxBarSize={36}>
                    <CartesianGrid stroke={GRID} vertical={false} />
                    <XAxis dataKey="label" tick={AXIS} axisLine={false} tickLine={false} />
                    <YAxis tickFormatter={millions} width={64} tick={AXIS} axisLine={false} tickLine={false} />
                    <Tooltip formatter={(v, name) => [money(Number(v)), name]} labelFormatter={(l) => `Tháng ${String(l).slice(1)}/${year}`} cursor={{ fill: 'rgba(0,0,0,0.04)' }} />
                    <Legend />
                    <Bar dataKey="collectedManual" name="Tại quầy / chuyển khoản" stackId="paid" fill={SERIES_1} stroke={SURFACE} strokeWidth={2} />
                    <Bar dataKey="collectedOnline" name="Qua PayOS" stackId="paid" fill={SERIES_2} stroke={SURFACE} strokeWidth={2} radius={[4, 4, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            ) : (
              <Table rowKey="month" size="small" pagination={false} dataSource={data?.months ?? []} columns={[
                { title: 'Tháng', dataIndex: 'month', render: (m: number) => `${String(m).padStart(2, '0')}/${year}` },
                { title: 'Thực thu', dataIndex: 'collected', align: 'right', render: money },
                { title: 'Qua PayOS', dataIndex: 'collectedOnline', align: 'right', render: money },
                { title: 'Tại quầy / chuyển khoản', dataIndex: 'collectedManual', align: 'right', render: money },
                { title: 'Số lần thu', dataIndex: 'paymentCount', align: 'right' },
              ]} />
            )}
          </Card>
        </Col>
        <Col span={24}>
          <Card title="Hóa đơn còn nợ">
            <Table rowKey="invoiceId" size="small" dataSource={data?.debts ?? []} pagination={{ pageSize: 10, hideOnSinglePage: true }} scroll={{ x: 800 }}
              columns={[
                { title: 'Số', dataIndex: 'number', render: (v: string, d) => <Link to={`/staff/invoices/${d.invoiceId}`}>{v}</Link> },
                { title: 'Cư dân', render: (_, d) => `${d.residentCode} · ${d.residentName}` },
                { title: 'Kỳ', dataIndex: 'billingPeriod', render: monthOf },
                { title: 'Hạn', dataIndex: 'dueDate', render: dateOnly },
                { title: 'Còn nợ', dataIndex: 'remaining', align: 'right', render: money },
                { title: 'Quá hạn', dataIndex: 'daysOverdue', render: (v: number) => (v > 0 ? <Tag color="red">{v} ngày</Tag> : <Tag>Chưa tới hạn</Tag>) },
                { title: 'Trạng thái', dataIndex: 'status', render: (v: string) => <StatusTag kind="invoice" value={v} /> },
              ]} />
          </Card>
        </Col>
      </Row>
    </>
  );
}

function OccupancyTab() {
  const { message } = App.useApp();
  const [year, setYear] = useState(() => Number(todayVn().slice(0, 4)));
  const [data, setData] = useState<OccupancyReport>();
  const [view, setView] = useState<View>('chart');

  useEffect(() => {
    reportsApi.occupancy(year).then(setData).catch((e) => message.error(extractErrorMessage(e)));
  }, [year, message]);

  const months = (data?.months ?? []).map((m) => ({ ...m, label: `T${m.month}` }));

  return (
    <>
      <Space style={{ marginBottom: 16 }} wrap>
        <Select value={year} onChange={setYear} options={yearOptions()} style={{ width: 130 }} />
        <ExportButton onExport={() => reportsApi.exportOccupancy(year)} />
      </Space>
      <Row gutter={[16, 16]}>
        <Col xs={24} md={8}>
          <Card><Statistic title={`Công suất trung bình ${year} (tới hôm nay)`} value={data?.averagePercent ?? '—'} suffix={data?.averagePercent != null ? '%' : undefined} /></Card>
        </Col>
        <Col span={24}>
          <Card title="Công suất giường theo tháng (%) = giường-ngày có người / giường-ngày khả dụng" extra={<ViewToggle value={view} onChange={setView} />}>
            {view === 'chart' ? (
              <div style={{ height: 300 }}>
                <ResponsiveContainer>
                  <LineChart data={months} margin={{ top: 8, right: 16 }}>
                    <CartesianGrid stroke={GRID} vertical={false} />
                    <XAxis dataKey="label" tick={AXIS} axisLine={false} tickLine={false} />
                    <YAxis domain={[0, 100]} tickFormatter={(v: number) => `${v}%`} width={48} tick={AXIS} axisLine={false} tickLine={false} />
                    <Tooltip
                      formatter={(v) => [`${Number(v).toLocaleString('vi-VN')}%`, 'Công suất']}
                      labelFormatter={(l) => `Tháng ${String(l).slice(1)}/${year}`}
                    />
                    <Line type="monotone" dataKey="percent" name="Công suất" stroke={SERIES_1} strokeWidth={2}
                      dot={{ r: 4, fill: SERIES_1, stroke: SURFACE, strokeWidth: 2 }} activeDot={{ r: 6 }} connectNulls={false} />
                  </LineChart>
                </ResponsiveContainer>
              </div>
            ) : (
              <Table rowKey="month" size="small" pagination={false} dataSource={data?.months ?? []} columns={[
                { title: 'Tháng', dataIndex: 'month', render: (m: number) => `${String(m).padStart(2, '0')}/${year}` },
                { title: 'Giường-ngày có người', dataIndex: 'occupiedBedDays', align: 'right' },
                { title: 'Giường-ngày khả dụng', dataIndex: 'availableBedDays', align: 'right' },
                { title: 'Công suất', dataIndex: 'percent', align: 'right', render: (v?: number | null) => (v == null ? '—' : `${v.toLocaleString('vi-VN')}%`) },
              ]} />
            )}
          </Card>
        </Col>
      </Row>
    </>
  );
}

function AdherenceTab() {
  const { message } = App.useApp();
  const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [dayjs(todayVn()).subtract(29, 'day'), dayjs(todayVn())]);
  const [data, setData] = useState<AdherenceReport>();
  const from = range[0].format('YYYY-MM-DD');
  const to = range[1].format('YYYY-MM-DD');

  const load = useCallback(async () => {
    try {
      setData(await reportsApi.adherence(from, to));
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  }, [from, to, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const rows = data?.rows ?? [];

  return (
    <>
      <Space style={{ marginBottom: 16 }} wrap>
        <DatePicker.RangePicker value={range} format="DD/MM/YYYY" allowClear={false}
          onChange={(v) => { if (v?.[0] && v[1]) setRange([v[0], v[1]]); }}
          disabledDate={(d) => d.isAfter(dayjs(todayVn()), 'day')} />
        <ExportButton onExport={() => reportsApi.exportAdherence(from, to)} />
      </Space>
      <Alert type="info" showIcon style={{ marginBottom: 16 }} message="Dữ liệu thuốc trong bản demo là mô phỏng. Báo cáo chỉ để theo dõi việc ghi nhận liều, không dùng để chẩn đoán." />
      <Row gutter={[16, 16]}>
        <Col xs={24} md={8}>
          <Card><Statistic title="Tỉ lệ đã dùng (mọi cư dân)" value={data?.overallTakenRate ?? '—'} suffix={data?.overallTakenRate != null ? '%' : undefined} /></Card>
        </Col>
        <Col span={24}>
          <Card title="Số liều theo cư dân">
            {rows.length === 0 ? <Empty description="Không có liều nào được ghi nhận trong khoảng này." /> : (
              <div style={{ height: 56 + rows.length * 40 }}>
                <ResponsiveContainer>
                  <BarChart data={rows} layout="vertical" maxBarSize={22} margin={{ left: 8, right: 16 }}>
                    <CartesianGrid stroke={GRID} horizontal={false} />
                    <XAxis type="number" allowDecimals={false} tick={AXIS} axisLine={false} tickLine={false} />
                    <YAxis type="category" dataKey="code" width={72} tick={AXIS} axisLine={false} tickLine={false} />
                    <Tooltip
                      formatter={(v, name) => [`${v} liều`, name]}
                      labelFormatter={(code) => { const r = rows.find((x) => x.code === code); return r ? `${r.code} · ${r.fullName} — ${r.takenRate}% đã dùng` : String(code); }}
                      cursor={{ fill: 'rgba(0,0,0,0.04)' }}
                    />
                    <Legend />
                    <Bar dataKey="taken" name="Đã dùng" stackId="dose" fill={STATUS_GOOD} stroke={SURFACE} strokeWidth={2} />
                    <Bar dataKey="missed" name="Bỏ lỡ" stackId="dose" fill={STATUS_WARNING} stroke={SURFACE} strokeWidth={2} />
                    <Bar dataKey="refused" name="Từ chối" stackId="dose" fill={STATUS_CRITICAL} stroke={SURFACE} strokeWidth={2} radius={[0, 4, 4, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            )}
          </Card>
        </Col>
        <Col span={24}>
          <Card title="Chi tiết">
            <Table rowKey="residentId" size="small" dataSource={rows} pagination={false} scroll={{ x: 700 }}
              columns={[
                { title: 'Cư dân', render: (_, r) => <Link to={`/staff/residents/${r.residentId}`}>{r.code} · {r.fullName}</Link> },
                { title: 'Đã dùng', dataIndex: 'taken', align: 'right' },
                { title: 'Bỏ lỡ', dataIndex: 'missed', align: 'right' },
                { title: 'Từ chối', dataIndex: 'refused', align: 'right' },
                { title: 'Tổng liều', dataIndex: 'total', align: 'right' },
                {
                  title: 'Tỉ lệ đã dùng', dataIndex: 'takenRate', align: 'right',
                  render: (v: number) => <Typography.Text strong={v < 80}>{v.toLocaleString('vi-VN')}%</Typography.Text>,
                },
              ]} />
          </Card>
        </Col>
      </Row>
    </>
  );
}
