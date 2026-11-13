import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Alert, Button, Card, Descriptions, Table, Tabs, Timeline } from 'antd';
import { portalApi } from '../../api/portal';
import { extractErrorMessage } from '../../api/client';
import { PageTitle, StatusTag } from '../../components/Common';
import { VitalsChart } from '../../components/VitalsChart';
import { dateOnly, dateTime, hhmm, money, monthOf } from '../../utils/format';
import type { FamilyResidentDetail } from '../../types';

/** UC-101, UC-102: chỉ đọc; mỗi tab hiện theo quyền chia sẻ do lễ tân cấp. */
export default function FamilyResident() {
  const id = Number(useParams().id);
  const navigate = useNavigate();
  const [data, setData] = useState<FamilyResidentDetail>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    portalApi.familyResident(id).then(setData).catch((e) => setError(extractErrorMessage(e)));
  }, [id]);

  if (error) return <Alert type="error" showIcon title={error} />;
  if (!data) return <Card loading />;
  const r = data.resident;

  const tabs = [
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
        </>
      ),
    }] : []),
    ...(r.canViewCare ? [{
      key: 'care', label: 'Chăm sóc hằng ngày', children: data.careRecords.length === 0 ? <p>Chưa có ghi chép được chia sẻ.</p> : (
        <Timeline items={data.careRecords.map((c) => ({
          children: <><b>{c.taskTitle}</b> · {dateTime(c.performedAt)} · {c.recordedByName}<div>{c.result}</div></>,
        }))} />
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
