import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Alert, Button, Card, DatePicker, Descriptions, Space, Table, Tabs, Tag, Timeline, Typography } from 'antd';
import {
  CheckCircleOutlined,
  CoffeeOutlined,
  HeartOutlined,
  MedicineBoxOutlined,
  RobotOutlined,
  SmileOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { aiInsightsApi } from '../../api/aiInsights';
import { familyCareApi } from '../../api/familyCare';
import { incidentsApi } from '../../api/incidents';
import { medicalProfileApi } from '../../api/medicalProfile';
import { portalApi } from '../../api/portal';
import { extractErrorMessage } from '../../api/client';
import { PageTitle, StatusTag } from '../../components/Common';
import { VitalsChart } from '../../components/VitalsChart';
import { dateOnly, dateTime, dayjs, hhmm, money, monthOf, timeOf } from '../../utils/format';
import type { FamilyResidentDetail } from '../../types';
import type { AiInsight, FamilyIncident, MedicalProfile, TimelineItem, TimelineKind } from '../../types/care';

function timelineIcon(kind: TimelineKind) {
  switch (kind) {
    case 'Meal':
      return <CoffeeOutlined style={{ color: '#d46b08' }} />;
    case 'Adl':
      return <SmileOutlined style={{ color: '#0958d9' }} />;
    case 'Vital':
      return <HeartOutlined style={{ color: '#cf1322' }} />;
    case 'Incident':
      return <WarningOutlined style={{ color: '#cf1322' }} />;
    case 'DoctorVisit':
      return <MedicineBoxOutlined style={{ color: '#389e0d' }} />;
    default:
      return <CheckCircleOutlined style={{ color: '#1677ff' }} />;
  }
}

/** UC-101, UC-102, UC-108: chỉ đọc; mỗi tab hiện theo quyền chia sẻ do lễ tân cấp. */
export default function FamilyResident() {
  const id = Number(useParams().id);
  const navigate = useNavigate();
  const [data, setData] = useState<FamilyResidentDetail>();
  const [incidents, setIncidents] = useState<FamilyIncident[]>([]);
  const [medical, setMedical] = useState<MedicalProfile>();
  const [timelineDate, setTimelineDate] = useState<dayjs.Dayjs>(dayjs());
  const [timeline, setTimeline] = useState<TimelineItem[]>([]);
  const [digests, setDigests] = useState<AiInsight[]>([]);
  const [error, setError] = useState<string>();

  useEffect(() => {
    setIncidents([]);
    setMedical(undefined);
    portalApi
      .familyResident(id)
      .then((res) => {
        setData(res);
        if (res.resident.canViewCare) {
          incidentsApi.familyIncidents(id).then(setIncidents).catch(() => undefined);
        }
        if (res.resident.canViewHealth) {
          medicalProfileApi.familyGet(id).then(setMedical).catch(() => undefined);
        }
      })
      .catch((e) => setError(extractErrorMessage(e)));
  }, [id]);

  useEffect(() => {
    if (!data) return;
    if (!data.resident.canViewCare && !data.resident.canViewHealth) {
      setTimeline([]);
      setDigests([]);
      return;
    }
    const dateStr = timelineDate.format('YYYY-MM-DD');
    familyCareApi
      .timeline(id, dateStr)
      .then(setTimeline)
      .catch(() => setTimeline([]));
    if (data.resident.canViewCare) {
      aiInsightsApi
        .familyDigests(id, dateStr)
        .then(setDigests)
        .catch(() => setDigests([]));
    } else {
      setDigests([]);
    }
  }, [id, data, timelineDate]);

  if (error) return <Alert type="error" showIcon title={error} />;
  if (!data) return <Card loading />;
  const r = data.resident;

  const tabs = [
    ...((r.canViewCare || r.canViewHealth) ? [{
      key: 'timeline',
      label: 'Nhật ký hôm nay',
      children: (
        <>
          <Space style={{ marginBottom: 16 }}>
            <DatePicker
              value={timelineDate}
              onChange={(d) => d && setTimelineDate(d)}
              format="DD/MM/YYYY"
              allowClear={false}
              disabledDate={(current) =>
                current > dayjs().endOf('day') || current < dayjs().subtract(30, 'day').startOf('day')
              }
            />
          </Space>
          {digests.map((dg) => (
            <Card
              key={dg.id}
              size="small"
              style={{ marginBottom: 16, background: '#fafafa' }}
              title={
                <Space size={6}>
                  <RobotOutlined />
                  <span>Tóm tắt ngày {dateOnly(dg.forDate)}</span>
                  <Tag color="blue">Do AI soạn</Tag>
                </Space>
              }
            >
              <Typography.Paragraph style={{ marginBottom: 6, whiteSpace: 'pre-wrap' }}>
                {dg.content}
              </Typography.Paragraph>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {dg.disclaimer}
              </Typography.Text>
            </Card>
          ))}
          {timeline.length === 0 ? (
            <Typography.Text type="secondary">Chưa có ghi nhận được chia sẻ trong ngày này.</Typography.Text>
          ) : (
            <Timeline
              items={timeline.map((item) => ({
                dot: timelineIcon(item.kind),
                children: (
                  <>
                    <b>{timeOf(item.at)} · {item.title}</b>{' '}
                    <Typography.Text type="secondary">({item.recordedBy})</Typography.Text>
                    <div>{item.detail}</div>
                  </>
                ),
              }))}
            />
          )}
        </>
      ),
    }] : []),
    ...(r.canViewHealth ? [{
      key: 'health', label: 'Sức khỏe', children: (
        <>
          <VitalsChart vitals={data.vitals} />
          <Table rowKey="id" size="small" dataSource={data.vitals} pagination={{ pageSize: 8 }} style={{ marginTop: 12 }} columns={[
            { title: 'Lúc', dataIndex: 'recordedAt', render: dateTime },
            { title: 'Huyết áp', render: (_, v) => `${v.systolic}/${v.diastolic}` },
            { title: 'Mạch', dataIndex: 'heartRate' },
            { title: 'Nhiệt độ', dataIndex: 'temperature', render: (t: number) => `${t}°C` },
            { title: 'SpO2', dataIndex: 'spO2', render: (s?: number) => (s != null ? `${s}%` : '') },
            { title: 'Ghi chú', dataIndex: 'notes' },
          ]} />
          <Typography.Title level={5} style={{ marginTop: 20 }}>Bệnh nền, dị ứng, chế độ ăn</Typography.Title>
          <Card size="small" style={{ marginBottom: 12 }} title="Chế độ ăn hiện hành">
            {medical?.activeDiet ? (
              <Space wrap>
                <StatusTag kind="dietType" value={medical.activeDiet.dietType} />
                <StatusTag kind="texture" value={medical.activeDiet.texture} />
                {medical.activeDiet.restrictions && <span>{medical.activeDiet.restrictions}</span>}
              </Space>
            ) : (
              <span>Chưa có chế độ ăn riêng.</span>
            )}
          </Card>
          <Table
            rowKey="id"
            size="small"
            dataSource={medical?.conditions ?? []}
            pagination={false}
            style={{ marginBottom: 12 }}
            columns={[
              { title: 'Bệnh nền', dataIndex: 'name' },
              { title: 'Trạng thái', dataIndex: 'status', render: (v: string) => <StatusTag kind="conditionStatus" value={v} /> },
              { title: 'Ghi chú', dataIndex: 'notes' },
            ]}
          />
          <Table
            rowKey="id"
            size="small"
            dataSource={medical?.allergies ?? []}
            pagination={false}
            columns={[
              { title: 'Dị ứng', dataIndex: 'allergen' },
              { title: 'Loại', dataIndex: 'type', render: (v: string) => <StatusTag kind="allergyType" value={v} /> },
              { title: 'Mức độ', dataIndex: 'severity', render: (v: string) => <StatusTag kind="allergySeverity" value={v} /> },
              { title: 'Phản ứng', dataIndex: 'reaction' },
            ]}
          />
        </>
      ),
    }] : []),
    ...(r.canViewCare ? [{
      key: 'care', label: 'Chăm sóc hằng ngày', children: (
        <>
          {data.careRecords.length === 0 ? <p>Chưa có ghi chép được chia sẻ.</p> : (
            <Timeline items={data.careRecords.map((c) => ({
              children: <><b>{c.taskTitle}</b> · {dateTime(c.performedAt)} · {c.recordedByName}<div>{c.result}</div></>,
            }))} />
          )}
          <Typography.Title level={5} style={{ marginTop: 16 }}>Sự cố đã chia sẻ</Typography.Title>
          {incidents.length === 0 ? <p>Chưa có sự cố nào được chia sẻ.</p> : (
            <Table
              rowKey="id"
              size="small"
              dataSource={incidents}
              pagination={false}
              columns={[
                { title: 'Thời điểm', dataIndex: 'occurredAt', render: dateTime },
                {
                  title: 'Loại & mức độ',
                  render: (_, i) => (
                    <Space size={4}>
                      <StatusTag kind="incidentType" value={i.type} />
                      <StatusTag kind="incidentSeverity" value={i.severity} />
                    </Space>
                  ),
                },
                { title: 'Mô tả', dataIndex: 'description' },
                { title: 'Xử lý / kết luận', render: (_, i) => i.resolution || i.immediateAction || '—' },
                { title: 'Trạng thái', dataIndex: 'status', render: (v: string) => <StatusTag kind="incident" value={v} /> },
              ]}
            />
          )}
        </>
      ),
    }] : []),
    ...(r.canViewMedication ? [{
      key: 'med', label: 'Thuốc đang dùng', children: (
        <Table rowKey="id" size="small" dataSource={data.medications} pagination={false} columns={[
          { title: 'Thuốc', dataIndex: 'medicationName' },
          { title: 'Liều', dataIndex: 'dose' },
          { title: 'Giờ', dataIndex: 'scheduledTime', render: hhmm },
          { title: 'Từ ngày', dataIndex: 'startDate', render: dateOnly },
        ]} />
      ),
    }] : []),
    ...(r.canViewBilling ? [{
      key: 'billing', label: 'Hóa đơn', children: (
        <Table rowKey="id" size="small" dataSource={data.invoices} pagination={false} columns={[
          { title: 'Kỳ', dataIndex: 'billingPeriod', render: monthOf },
          { title: 'Số', dataIndex: 'number' },
          { title: 'Tổng', dataIndex: 'total', render: money },
          { title: 'Còn nợ', dataIndex: 'remaining', render: money },
          { title: 'Trạng thái', dataIndex: 'status', render: (v: string) => <StatusTag kind="invoice" value={v} /> },
          {
            title: '', key: 'pay', render: (_, i) => (
              <Button size="small" type={i.remaining > 0 && i.status !== 'Cancelled' ? 'primary' : 'default'} onClick={() => navigate(`/family/invoices/${i.id}`)}>
                {i.remaining > 0 && i.status !== 'Cancelled' ? 'Thanh toán' : 'Xem'}
              </Button>
            ),
          },
        ]} />
      ),
    }] : []),
  ];

  return (
    <>
      <PageTitle title={r.fullName} />
      <Card style={{ marginBottom: 16 }}>
        <Descriptions size="small" column={{ xs: 1, md: 3 }}>
          <Descriptions.Item label="Mã">{r.code}</Descriptions.Item>
          <Descriptions.Item label="Ngày sinh">{dateOnly(r.dateOfBirth)}</Descriptions.Item>
          <Descriptions.Item label="Phòng">{data.bedLabel ?? '—'}</Descriptions.Item>
        </Descriptions>
      </Card>
      <Card>{tabs.length ? <Tabs items={tabs} /> : <p>Bạn chưa được cấp quyền xem thông tin chi tiết.</p>}</Card>
    </>
  );
}
