import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { App, Button, DatePicker, Segmented, Space, Switch, Table } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { careApi } from '../../api/care';
import { extractErrorMessage } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { CARE_PLANNERS, hasRole } from '../../auth/roles';
import { CreateTaskModal, TaskRecordsDrawer, UpdateTaskModal } from '../../components/CareTaskModals';
import { PageTitle, StatusTag } from '../../components/Common';
import { dayjs, timeOf } from '../../utils/format';
import type { CareTask } from '../../types';

/** UC-52..UC-55: việc chăm sóc theo ngày. Caregiver luôn chỉ thấy việc của mình (API lọc). */
export default function CareTasks() {
  const { message } = App.useApp();
  const { user } = useAuth();
  const isCaregiver = user?.role === 'Caregiver';
  const [date, setDate] = useState(dayjs());
  const [mine, setMine] = useState(isCaregiver || user?.role === 'Nurse');
  const [status, setStatus] = useState('open');
  const [rows, setRows] = useState<CareTask[]>([]);
  const [loading, setLoading] = useState(false);
  const [creating, setCreating] = useState(false);
  const [updating, setUpdating] = useState<CareTask | null>(null);
  const [viewing, setViewing] = useState<CareTask | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const filter = status === 'open' ? ["(status eq 'Pending' or status eq 'InProgress')"] : [];
      const page = await careApi.tasks({ params: { date: date.format('YYYY-MM-DD'), mine }, filter, orderBy: 'dueAt' });
      setRows(page.items);
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [date, mine, status, message]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <>
      <PageTitle
        title="Công việc chăm sóc"
        extra={hasRole(user?.role, CARE_PLANNERS) && <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreating(true)}>Giao việc</Button>}
      />
      <Space style={{ marginBottom: 16 }} wrap>
        <DatePicker value={date} onChange={(d) => d && setDate(d)} format="DD/MM/YYYY" allowClear={false} />
        <Segmented value={status} onChange={(v) => setStatus(String(v))} options={[{ value: 'open', label: 'Chưa xong' }, { value: 'all', label: 'Tất cả' }]} />
        {!isCaregiver && <span><Switch checked={mine} onChange={setMine} /> Chỉ việc của tôi</span>}
      </Space>
      <Table
        rowKey="id"
        loading={loading}
        dataSource={rows}
        pagination={false}
        columns={[
          { title: 'Giờ', dataIndex: 'dueAt', render: timeOf, width: 70 },
          { title: 'Cư dân', render: (_, t) => <Link to={`/staff/residents/${t.residentId}`}>{t.residentCode} · {t.residentName}</Link> },
          { title: 'Công việc', dataIndex: 'title' },
          { title: 'Người làm', dataIndex: 'assignedToName' },
          { title: 'Trạng thái', dataIndex: 'status', render: (v: string) => <StatusTag kind="care" value={v} /> },
          {
            title: '', key: 'a', render: (_, t) => (
              <Space>
                {(t.status === 'Pending' || t.status === 'InProgress') && <Button size="small" type="primary" onClick={() => setUpdating(t)}>Cập nhật</Button>}
                <Button size="small" onClick={() => setViewing(t)}>Lịch sử</Button>
              </Space>
            ),
          },
        ]}
      />
      <CreateTaskModal open={creating} onClose={() => setCreating(false)} onSaved={() => { setCreating(false); void load(); }} />
      <UpdateTaskModal task={updating} onClose={() => setUpdating(null)} onSaved={() => { setUpdating(null); void load(); }} />
      <TaskRecordsDrawer task={viewing} onClose={() => setViewing(null)} />
    </>
  );
}
