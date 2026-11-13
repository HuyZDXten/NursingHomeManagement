import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { App, Button, Input, Segmented, Space, Table } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { extractErrorMessage } from '../../api/client';
import { containsAny } from '../../api/odata';
import { residentsApi } from '../../api/residents';
import { useAuth } from '../../auth/AuthContext';
import { FRONT_DESK, hasRole } from '../../auth/roles';
import { PageTitle, StatusTag } from '../../components/Common';
import { dateOnly, dateTime } from '../../utils/format';
import type { ResidentListItem } from '../../types';
import { ResidentFormModal } from './resident/ResidentFormModal';

const PAGE_SIZE = 10;

/** UC-25: tìm kiếm, lọc, phân trang cư dân (OData phía server; Caregiver chỉ thấy người được giao). */
export default function Residents() {
  const { message } = App.useApp();
  const { user } = useAuth();
  const navigate = useNavigate();
  const [rows, setRows] = useState<ResidentListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [keyword, setKeyword] = useState('');
  const [status, setStatus] = useState<string>('Active');
  const [loading, setLoading] = useState(false);
  const [creating, setCreating] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await residentsApi.list({
        filter: [containsAny(['fullName', 'code'], keyword) ?? '', status === 'All' ? '' : `status eq '${status}'`],
        orderBy: 'code', top: PAGE_SIZE, skip: (page - 1) * PAGE_SIZE,
      });
      setRows(result.items);
      setTotal(result.total);
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [keyword, status, page, message]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <>
      <PageTitle
        title="Cư dân"
        extra={hasRole(user?.role, FRONT_DESK) && <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreating(true)}>Tiếp nhận cư dân</Button>}
      />
      <Space style={{ marginBottom: 16 }} wrap>
        <Input.Search placeholder="Tìm mã hoặc họ tên" allowClear onSearch={(v) => { setPage(1); setKeyword(v); }} style={{ width: 260 }} />
        <Segmented
          value={status}
          onChange={(v) => { setPage(1); setStatus(String(v)); }}
          options={[{ value: 'Active', label: 'Đang lưu trú' }, { value: 'Discharged', label: 'Đã ngừng' }, { value: 'All', label: 'Tất cả' }]}
        />
      </Space>
      <Table
        rowKey="id"
        loading={loading}
        dataSource={rows}
        onRow={(r) => ({ onClick: () => navigate(`/staff/residents/${r.id}`), style: { cursor: 'pointer' } })}
        pagination={{ current: page, pageSize: PAGE_SIZE, total, onChange: setPage, showTotal: (t) => `${t} cư dân` }}
        columns={[
          { title: 'Mã', dataIndex: 'code', width: 110 },
          { title: 'Họ tên', dataIndex: 'fullName' },
          { title: 'Tuổi', dataIndex: 'age', width: 70 },
          { title: 'Giới tính', dataIndex: 'gender', width: 90 },
          { title: 'Ngày sinh', dataIndex: 'dateOfBirth', render: dateOnly },
          { title: 'Giường', dataIndex: 'bedLabel', render: (v?: string) => v ?? '—' },
          { title: 'Nhập viện', dataIndex: 'admittedAt', render: dateTime },
          { title: 'Trạng thái', dataIndex: 'status', render: (v: string) => <StatusTag kind="resident" value={v} /> },
        ]}
      />
      <ResidentFormModal
        open={creating}
        onClose={() => setCreating(false)}
        onSaved={(r) => { setCreating(false); navigate(`/staff/residents/${r.id}`); }}
      />
    </>
  );
}
