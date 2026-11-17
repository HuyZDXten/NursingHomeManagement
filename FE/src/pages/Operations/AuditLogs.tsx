import { useCallback, useEffect, useState } from 'react';
import { Alert, App, Button, DatePicker, Input, Select, Space, Table, Tag, Typography } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import { extractErrorMessage } from '../../api/client';
import { systemApi } from '../../api/system';
import { PageTitle } from '../../components/Common';
import { dateTime, dayjs, todayVn } from '../../utils/format';
import type { AuditLog, PageResult } from '../../types/system';

const prettyJson = (json?: string | null) => {
  if (!json) return '';
  try {
    return JSON.stringify(JSON.parse(json), null, 2);
  } catch {
    return json;
  }
};

/** BL-V5, UC-13 (Admin): nhật ký thao tác. Chỉ đọc; hệ thống không có đường sửa hay xóa nhật ký. */
export default function AuditLogs() {
  const { message } = App.useApp();
  const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [dayjs(todayVn()).subtract(6, 'day'), dayjs(todayVn())]);
  const [action, setAction] = useState<string>();
  const [search, setSearch] = useState('');
  const [actions, setActions] = useState<string[]>([]);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);
  const [data, setData] = useState<PageResult<AuditLog>>();
  const [loading, setLoading] = useState(false);
  const from = range[0].format('YYYY-MM-DD');
  const to = range[1].format('YYYY-MM-DD');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setData(await systemApi.auditLogs({ from, to, action, search: search.trim() || undefined, page, pageSize }));
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [from, to, action, search, page, pageSize, message]);
  useEffect(() => {
    void load();
  }, [load]);
  useEffect(() => {
    systemApi.auditActions().then(setActions).catch(() => undefined);
  }, []);

  return (
    <>
      <PageTitle title="Nhật ký thao tác" extra={<Button icon={<ReloadOutlined />} onClick={() => void load()}>Tải lại</Button>} />
      <Alert type="info" showIcon style={{ marginBottom: 16 }}
        title="Ghi các thao tác quản trị đã thành công (tài khoản, phòng, hóa đơn, tiền cọc, hồ sơ nhân sự, cấu hình, tin tức...). Không ghi nội dung nhập vào như mật khẩu hay CCCD, và không thể sửa hay xóa nhật ký." />
      <Space style={{ marginBottom: 16 }} wrap>
        <DatePicker.RangePicker value={range} format="DD/MM/YYYY" allowClear={false}
          onChange={(v) => { if (v?.[0] && v[1]) { setRange([v[0], v[1]]); setPage(1); } }} />
        <Select allowClear showSearch style={{ width: 240 }} placeholder="Mọi thao tác" value={action} options={actions.map((a) => ({ value: a, label: a }))}
          onChange={(v) => { setAction(v); setPage(1); }} />
        <Input.Search allowClear style={{ width: 260 }} placeholder="Tìm theo mô tả hoặc người thực hiện" onSearch={(v) => { setSearch(v); setPage(1); }} />
      </Space>
      <Table<AuditLog> rowKey="id" size="small" loading={loading} dataSource={data?.items ?? []} scroll={{ x: 900 }}
        pagination={{
          current: page, pageSize, total: data?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100], showTotal: (total) => `${total} dòng`,
          onChange: (p, size) => { setPage(p); setPageSize(size); },
        }}
        expandable={{
          rowExpandable: (row) => !!row.dataJson,
          expandedRowRender: (row) => <Typography.Paragraph style={{ margin: 0 }}><pre style={{ margin: 0 }}>{prettyJson(row.dataJson)}</pre></Typography.Paragraph>,
        }}
        columns={[
          { title: 'Thời điểm', dataIndex: 'at', width: 150, render: (v: string) => dateTime(v) },
          { title: 'Người thực hiện', dataIndex: 'actorName', width: 170 },
          { title: 'Thao tác', render: (_, row) => <>{row.summary}<br /><Tag style={{ marginTop: 4 }}>{row.action}</Tag></> },
          { title: 'Đối tượng', width: 150, render: (_, row) => `${row.entityType}${row.entityId ? ` #${row.entityId}` : ''}` },
          { title: 'IP', dataIndex: 'ip', width: 120 },
        ]} />
    </>
  );
}
