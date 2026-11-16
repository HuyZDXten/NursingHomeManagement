import { useCallback, useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { App, Button, DatePicker, Select, Space, Table } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { incidentsApi } from '../../api/incidents';
import { extractErrorMessage } from '../../api/client';
import {
  INCIDENT_SEVERITY_OPTIONS,
  IncidentDetailDrawer,
  ReportIncidentModal,
} from '../../components/IncidentModals';
import { PageTitle, StatusTag } from '../../components/Common';
import { dateTime, dayjs } from '../../utils/format';
import type { Incident, IncidentSeverity, IncidentStatus } from '../../types/care';

const STATUS_OPTIONS: { value: IncidentStatus; label: string }[] = [
  { value: 'Open', label: 'Mới báo' },
  { value: 'Investigating', label: 'Đang xử lý' },
  { value: 'Closed', label: 'Đã đóng' },
];

/** S3-H1 (UC-58, UC-59): danh sách & xử lý sự cố. */
export default function Incidents() {
  const { message } = App.useApp();
  const [searchParams] = useSearchParams();
  const [status, setStatus] = useState<IncidentStatus>();
  const [severity, setSeverity] = useState<IncidentSeverity>();
  const [range, setRange] = useState<[dayjs.Dayjs | null, dayjs.Dayjs | null] | null>(null);
  const [rows, setRows] = useState<Incident[]>([]);
  const [loading, setLoading] = useState(false);
  const [reporting, setReporting] = useState(false);
  const [selected, setSelected] = useState<Incident | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await incidentsApi.list({
        status,
        severity,
        from: range?.[0]?.format('YYYY-MM-DD'),
        to: range?.[1]?.format('YYYY-MM-DD'),
      });
      setRows(data);
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [status, severity, range, message]);

  useEffect(() => {
    void load();
  }, [load]);

  // Mở sẵn drawer nếu đi từ thông báo ?id=123
  useEffect(() => {
    const idParam = Number(searchParams.get('id'));
    if (idParam > 0) {
      incidentsApi.get(idParam).then(setSelected).catch(() => undefined);
    }
  }, [searchParams]);

  return (
    <>
      <PageTitle
        title="Quản lý sự cố"
        extra={
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setReporting(true)}>
            Báo sự cố
          </Button>
        }
      />

      <Space style={{ marginBottom: 16 }} wrap>
        <Select
          allowClear
          placeholder="Trạng thái"
          style={{ width: 150 }}
          value={status}
          onChange={setStatus}
          options={STATUS_OPTIONS}
        />
        <Select
          allowClear
          placeholder="Mức độ"
          style={{ width: 140 }}
          value={severity}
          onChange={setSeverity}
          options={INCIDENT_SEVERITY_OPTIONS}
        />
        <DatePicker.RangePicker
          value={range}
          onChange={(vals) => setRange(vals)}
          format="DD/MM/YYYY"
        />
      </Space>

      <Table
        rowKey="id"
        loading={loading}
        dataSource={rows}
        pagination={{ pageSize: 15 }}
        onRow={(record) => ({
          onClick: () => setSelected(record),
          style: { cursor: 'pointer' },
        })}
        columns={[
          { title: 'Thời điểm', dataIndex: 'occurredAt', render: dateTime, width: 150 },
          {
            title: 'Cư dân',
            render: (_, r) => (
              <Link to={`/staff/residents/${r.residentId}`} onClick={(e) => e.stopPropagation()}>
                {r.residentCode} · {r.residentName}
              </Link>
            ),
          },
          {
            title: 'Loại',
            dataIndex: 'type',
            render: (v: string) => <StatusTag kind="incidentType" value={v} />,
          },
          {
            title: 'Mức độ',
            dataIndex: 'severity',
            render: (v: string) => <StatusTag kind="incidentSeverity" value={v} />,
          },
          { title: 'Mô tả', dataIndex: 'description', ellipsis: true },
          { title: 'Người báo', dataIndex: 'reportedByName', width: 150 },
          {
            title: 'Trạng thái',
            dataIndex: 'status',
            render: (v: string) => <StatusTag kind="incident" value={v} />,
          },
          {
            title: '',
            key: 'action',
            width: 90,
            render: (_, r) => (
              <Button size="small" onClick={(e) => { e.stopPropagation(); setSelected(r); }}>
                Chi tiết
              </Button>
            ),
          },
        ]}
      />

      <ReportIncidentModal
        open={reporting}
        onClose={() => setReporting(false)}
        onSaved={() => {
          setReporting(false);
          void load();
        }}
      />

      <IncidentDetailDrawer
        incident={selected}
        onClose={() => setSelected(null)}
        onUpdated={(next) => {
          setSelected(next);
          void load();
        }}
      />
    </>
  );
}
