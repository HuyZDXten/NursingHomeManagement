import { useCallback, useEffect, useState } from 'react';
import { App, Badge, Button, DatePicker, Input, Popconfirm, Select, Space, Table } from 'antd';
import { extractErrorMessage } from '../../api/client';
import { visitsApi } from '../../api/visits';
import { PageTitle, StatusTag } from '../../components/Common';
import { dayjs, hhmm } from '../../utils/format';
import type { VisitBooking, VisitStatus } from '../../types/care';

const STATUS_OPTIONS: { value: VisitStatus; label: string }[] = [
  { value: 'Requested', label: 'Chờ duyệt' },
  { value: 'Approved', label: 'Đã duyệt' },
  { value: 'CheckedIn', label: 'Đã đến' },
  { value: 'Completed', label: 'Hoàn tất' },
  { value: 'Rejected', label: 'Từ chối' },
  { value: 'Cancelled', label: 'Đã hủy' },
  { value: 'NoShow', label: 'Không đến' },
];

/** S3-H3 (UC-104): lễ tân duyệt, check-in, hoàn tất lịch thăm theo ngày. */
export default function Visits() {
  const { message, modal } = App.useApp();
  const [date, setDate] = useState<dayjs.Dayjs>(dayjs());
  const [status, setStatus] = useState<VisitStatus>();
  const [rows, setRows] = useState<VisitBooking[]>([]);
  const [loading, setLoading] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await visitsApi.listByDate(date.format('YYYY-MM-DD'), status));
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [date, status, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const act = async (fn: () => Promise<unknown>, okMsg: string) => {
    try {
      await fn();
      message.success(okMsg);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  const openReject = (b: VisitBooking) => {
    let reason = '';
    modal.confirm({
      title: `Từ chối lịch thăm của ${b.familyName}?`,
      content: (
        <Input.TextArea
          rows={3}
          placeholder="Nhập lý do từ chối (bắt buộc)"
          onChange={(e) => {
            reason = e.target.value;
          }}
        />
      ),
      okText: 'Từ chối',
      okButtonProps: { danger: true },
      onOk: async () => {
        if (!reason.trim()) {
          message.warning('Vui lòng nhập lý do từ chối.');
          return Promise.reject();
        }
        await visitsApi.reject(b.id, reason);
        message.success('Đã từ chối lịch thăm.');
        void load();
      },
    });
  };

  const pendingCount = rows.filter((r) => r.status === 'Requested').length;

  return (
    <>
      <PageTitle
        title={
          <Space>
            <span>Lịch thăm gia đình</span>
            <Badge count={pendingCount} showZero={false} title="Lịch chờ duyệt trong ngày" />
          </Space>
        }
      />

      <Space style={{ marginBottom: 16 }} wrap>
        <DatePicker value={date} onChange={(d) => d && setDate(d)} format="DD/MM/YYYY" allowClear={false} />
        <Select
          allowClear
          placeholder="Trạng thái"
          style={{ width: 160 }}
          value={status}
          onChange={setStatus}
          options={STATUS_OPTIONS}
        />
      </Space>

      <Table
        rowKey="id"
        loading={loading}
        dataSource={rows}
        pagination={false}
        columns={[
          { title: 'Khung giờ', render: (_, b) => `${hhmm(b.slotStart)}–${hhmm(b.slotEnd)}`, width: 120 },
          { title: 'Cư dân', render: (_, b) => `${b.residentCode} · ${b.residentName}` },
          { title: 'Người đặt', render: (_, b) => `${b.familyName}${b.familyPhone ? ` (${b.familyPhone})` : ''}` },
          { title: 'Số khách', dataIndex: 'guestCount', width: 90 },
          { title: 'Tên khách', dataIndex: 'guestNames' },
          { title: 'Trạng thái', dataIndex: 'status', render: (v: string) => <StatusTag kind="visit" value={v} /> },
          {
            title: 'Thao tác',
            key: 'actions',
            render: (_, b) => (
              <Space wrap>
                {b.status === 'Requested' && (
                  <>
                    <Button size="small" type="primary" onClick={() => act(() => visitsApi.approve(b.id), 'Đã duyệt lịch thăm.')}>
                      Duyệt
                    </Button>
                    <Button size="small" danger onClick={() => openReject(b)}>
                      Từ chối
                    </Button>
                  </>
                )}
                {b.status === 'Approved' && (
                  <>
                    <Button size="small" type="primary" onClick={() => act(() => visitsApi.checkIn(b.id), 'Đã check-in khách.')}>
                      Check-in
                    </Button>
                    <Popconfirm
                      title="Đánh dấu khách không đến?"
                      onConfirm={() => act(() => visitsApi.noShow(b.id), 'Đã đánh dấu không đến.')}
                    >
                      <Button size="small">Không đến</Button>
                    </Popconfirm>
                  </>
                )}
                {b.status === 'CheckedIn' && (
                  <Button size="small" onClick={() => act(() => visitsApi.complete(b.id), 'Đã hoàn tất lượt thăm.')}>
                    Hoàn tất
                  </Button>
                )}
              </Space>
            ),
          },
        ]}
      />
    </>
  );
}
