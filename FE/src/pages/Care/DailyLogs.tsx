import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { App, Button, DatePicker, Space, Table, Tag } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { extractErrorMessage } from '../../api/client';
import { dailyLogsApi } from '../../api/carePlans';
import { residentsApi } from '../../api/residents';
import { PageTitle } from '../../components/Common';
import { AdlModal, MealModal } from '../Staff/resident/CarePlanTabs';
import { dayjs } from '../../utils/format';
import type { ResidentListItem } from '../../types';
import type { DailyCareLog, MealLog, MealType } from '../../types/care';

interface ResidentRow {
  resident: ResidentListItem;
  meals: MealLog[];
  logs: DailyCareLog[];
}

const MEALS: { key: MealType; label: string }[] = [
  { key: 'Breakfast', label: 'Sáng' },
  { key: 'Lunch', label: 'Trưa' },
  { key: 'Dinner', label: 'Tối' },
  { key: 'Snack', label: 'Phụ' },
];

/** S3-H4 (UC-56, UC-57): trang ghi nhanh ADL & bữa ăn cho các cư dân được giao hôm nay. */
export default function DailyLogs() {
  const { message } = App.useApp();
  const [date, setDate] = useState<dayjs.Dayjs>(dayjs());
  const [rows, setRows] = useState<ResidentRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [mealModal, setMealModal] = useState<{ residentId: number; meal: MealType; existing?: MealLog } | null>(null);
  const [adlResidentId, setAdlResidentId] = useState<number | null>(null);

  const dateStr = date.format('YYYY-MM-DD');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const resPage = await residentsApi.list({ filter: ["status eq 'Active'"], orderBy: 'fullName', top: 100 });
      const items = await Promise.all(
        resPage.items.map(async (r) => {
          const [meals, logs] = await Promise.all([
            dailyLogsApi.listMeals(r.id, dateStr).catch(() => [] as MealLog[]),
            dailyLogsApi.listLogs(r.id, dateStr).catch(() => [] as DailyCareLog[]),
          ]);
          return { resident: r, meals, logs };
        }),
      );
      setRows(items);
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [dateStr, message]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <>
      <PageTitle
        title="Ghi nhận hằng ngày (Bữa ăn & ADL)"
        extra={<DatePicker value={date} onChange={(d) => d && setDate(d)} format="DD/MM/YYYY" allowClear={false} />}
      />

      <Table
        rowKey={(r) => r.resident.id}
        loading={loading}
        dataSource={rows}
        pagination={false}
        columns={[
          {
            title: 'Cư dân',
            render: (_, row) => (
              <Link to={`/staff/residents/${row.resident.id}`}>
                {row.resident.code} · {row.resident.fullName}
              </Link>
            ),
          },
          {
            title: 'Phòng',
            render: (_, row) => row.resident.bedLabel ?? '—',
            width: 140,
          },
          {
            title: 'Bữa ăn hôm nay',
            render: (_, row) => (
              <Space wrap>
                {MEALS.map((m) => {
                  const found = row.meals.find((x) => x.meal === m.key);
                  return (
                    <Button
                      key={m.key}
                      size="small"
                      type={found ? 'default' : 'dashed'}
                      onClick={() => setMealModal({ residentId: row.resident.id, meal: m.key, existing: found })}
                    >
                      {m.label}: {found ? `${found.intakePercent}%` : '—'}
                    </Button>
                  );
                })}
              </Space>
            ),
          },
          {
            title: 'ADL',
            render: (_, row) => (
              <Space>
                <Tag color={row.logs.length > 0 ? 'green' : 'default'}>{row.logs.length} mục</Tag>
                <Button size="small" icon={<PlusOutlined />} onClick={() => setAdlResidentId(row.resident.id)}>
                  Ghi ADL
                </Button>
              </Space>
            ),
            width: 180,
          },
        ]}
      />

      {mealModal && (
        <MealModal
          open
          residentId={mealModal.residentId}
          date={dateStr}
          meal={mealModal.meal}
          existing={mealModal.existing}
          onClose={() => setMealModal(null)}
          onSaved={() => {
            setMealModal(null);
            void load();
          }}
        />
      )}
      {adlResidentId !== null && (
        <AdlModal
          open
          residentId={adlResidentId}
          date={dateStr}
          onClose={() => setAdlResidentId(null)}
          onSaved={() => {
            setAdlResidentId(null);
            void load();
          }}
        />
      )}
    </>
  );
}
