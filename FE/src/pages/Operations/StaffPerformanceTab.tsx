import { useCallback, useEffect, useState } from 'react';
import { Alert, App, Button, Card, Col, DatePicker, Empty, Row, Space, Statistic, Table, Typography } from 'antd';
import { DownloadOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import { extractErrorMessage } from '../../api/client';
import { staffHrApi } from '../../api/staffHr';
import { ROLE_LABEL } from '../../utils/status';
import { dayjs, todayVn } from '../../utils/format';
import type { StaffPerformanceReport, StaffPerformanceRow } from '../../types/operations';

const rate = (value?: number | null) => (value == null ? '—' : `${value.toLocaleString('vi-VN')}%`);

/** UC-126 (Admin): hiệu suất nhân viên = việc chăm sóc xong đúng hạn + chuyên cần (đi muộn, vắng). Chỉ đọc dữ liệu việc và thuốc. */
export function StaffPerformanceTab() {
  const { message } = App.useApp();
  const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [dayjs(todayVn()).subtract(29, 'day'), dayjs(todayVn())]);
  const [data, setData] = useState<StaffPerformanceReport>();
  const [exporting, setExporting] = useState(false);
  const from = range[0].format('YYYY-MM-DD');
  const to = range[1].format('YYYY-MM-DD');

  const load = useCallback(async () => {
    try {
      setData(await staffHrApi.performance(from, to));
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  }, [from, to, message]);
  useEffect(() => {
    void load();
  }, [load]);

  const rows = data?.rows ?? [];
  const tasks = rows.reduce((n, r) => n + r.tasksDue, 0);
  const onTime = rows.reduce((n, r) => n + r.tasksOnTime, 0);
  const shifts = rows.reduce((n, r) => n + r.shiftsAssigned, 0);
  const attended = rows.reduce((n, r) => n + r.shiftsAttended, 0);

  return (
    <>
      <Space style={{ marginBottom: 16 }} wrap>
        <DatePicker.RangePicker value={range} format="DD/MM/YYYY" allowClear={false}
          onChange={(v) => { if (v?.[0] && v[1]) setRange([v[0], v[1]]); }} disabledDate={(d) => d.isAfter(dayjs(todayVn()), 'day')} />
        <Button icon={<DownloadOutlined />} loading={exporting} onClick={async () => {
          setExporting(true);
          try {
            await staffHrApi.exportPerformance(from, to);
          } catch (e) {
            message.error(extractErrorMessage(e));
          } finally {
            setExporting(false);
          }
        }}>Xuất Excel</Button>
      </Space>
      <Alert type="info" showIcon style={{ marginBottom: 16 }}
        title="Đúng hạn = hoàn thành trong hạn + 15 phút. Chỉ tính việc đã tới hạn và ca đã kết thúc. Đây là số liệu tham khảo để trao đổi với nhân viên, không phải căn cứ kỷ luật." />
      <Row gutter={[16, 16]}>
        <Col xs={12} md={6}><Card><Statistic title="Việc đúng hạn (toàn bộ)" value={tasks ? Math.round((onTime * 1000) / tasks) / 10 : '—'} suffix={tasks ? '%' : undefined} /></Card></Col>
        <Col xs={12} md={6}><Card><Statistic title="Chuyên cần (toàn bộ)" value={shifts ? Math.round((attended * 1000) / shifts) / 10 : '—'} suffix={shifts ? '%' : undefined} /></Card></Col>
        <Col xs={12} md={6}><Card><Statistic title="Lần đi muộn" value={rows.reduce((n, r) => n + r.lateCount, 0)} /></Card></Col>
        <Col xs={12} md={6}><Card><Statistic title="Ca vắng" value={rows.reduce((n, r) => n + r.absent, 0)} /></Card></Col>
        <Col span={24}>
          <Card title="Chi tiết theo nhân viên">
            {rows.length === 0 ? <Empty description="Không có việc, ca trực hay ghi nhận thuốc nào trong khoảng này." /> : (
              <Table<StaffPerformanceRow> rowKey="accountId" size="small" dataSource={rows} pagination={false} scroll={{ x: 1000 }} columns={[
                { title: 'Nhân viên', render: (_, r) => <>{r.fullName}<br /><small>{ROLE_LABEL[r.role as keyof typeof ROLE_LABEL] ?? r.role}</small></> },
                { title: 'Việc có hạn', dataIndex: 'tasksDue', align: 'right' },
                { title: 'Đúng hạn', dataIndex: 'tasksOnTime', align: 'right' },
                { title: 'Trễ', dataIndex: 'tasksLate', align: 'right' },
                { title: 'Quá hạn chưa xong', dataIndex: 'tasksOverdueOpen', align: 'right', render: (v: number) => <Typography.Text type={v ? 'danger' : undefined}>{v}</Typography.Text> },
                { title: 'Đúng hạn %', dataIndex: 'onTimeRate', align: 'right', render: (v: number | null) => <Typography.Text strong={v != null && v < 80}>{rate(v)}</Typography.Text> },
                { title: 'Ghi thuốc', dataIndex: 'medicationRecords', align: 'right' },
                { title: 'Ca đã xong', dataIndex: 'shiftsAssigned', align: 'right' },
                { title: 'Có công', dataIndex: 'shiftsAttended', align: 'right' },
                { title: 'Muộn', dataIndex: 'lateCount', align: 'right' },
                { title: 'Vắng', dataIndex: 'absent', align: 'right', render: (v: number) => <Typography.Text type={v ? 'danger' : undefined}>{v}</Typography.Text> },
                { title: 'Chuyên cần %', dataIndex: 'attendanceRate', align: 'right', render: (v: number | null) => <Typography.Text strong={v != null && v < 90}>{rate(v)}</Typography.Text> },
              ]} />
            )}
          </Card>
        </Col>
      </Row>
    </>
  );
}
