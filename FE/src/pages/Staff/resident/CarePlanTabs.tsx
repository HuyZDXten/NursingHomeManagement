import { useCallback, useEffect, useState } from 'react';
import {
  App,
  Button,
  Card,
  Checkbox,
  DatePicker,
  Form,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Radio,
  Select,
  Space,
  Table,
  Tag,
  TimePicker,
  Typography,
} from 'antd';
import { DeleteOutlined, PlusOutlined, ThunderboltOutlined } from '@ant-design/icons';
import { carePlansApi, dailyLogsApi, type SaveCarePlanForm } from '../../../api/carePlans';
import { extractErrorMessage } from '../../../api/client';
import { useAuth } from '../../../auth/AuthContext';
import { CARE_PLANNERS, hasRole } from '../../../auth/roles';
import { AccountSelect, StatusTag } from '../../../components/Common';
import { dateOnly, dateTime, dayjs, hhmm } from '../../../utils/format';
import type {
  CarePlan,
  DailyCareCategory,
  DailyCareLevel,
  DailyCareLog,
  MealLog,
  MealType,
} from '../../../types/care';

const DAY_LABELS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];

function patternToDays(pattern: string): number[] {
  const days: number[] = [];
  for (let i = 0; i < 7; i++) {
    if (pattern[i] === '1') days.push(i);
  }
  return days;
}

function daysToPattern(days: number[] = []): string {
  return Array.from({ length: 7 }, (_, i) => (days.includes(i) ? '1' : '0')).join('');
}

function formatDays(pattern: string): string {
  if (pattern === '1111111') return 'Hằng ngày';
  return DAY_LABELS.filter((_, i) => pattern[i] === '1').join(', ');
}

// ======================= S3-H4 (UC-51): Kế hoạch chăm sóc =======================
export function CarePlansTab({ residentId, active }: { residentId: number; active: boolean }) {
  const { message } = App.useApp();
  const { user } = useAuth();
  const canPlan = active && hasRole(user?.role, CARE_PLANNERS);

  const [plans, setPlans] = useState<CarePlan[]>([]);
  const [editing, setEditing] = useState<CarePlan | 'new' | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [form] = Form.useForm();

  const load = useCallback(() => {
    carePlansApi.listByResident(residentId).then(setPlans).catch(() => undefined);
  }, [residentId]);

  useEffect(() => {
    void load();
  }, [load]);

  const generateNow = async () => {
    setGenerating(true);
    try {
      const res = await carePlansApi.generate(2, residentId);
      message.success(
        `Đã sinh ${res.createdCount} công việc (hôm nay & ngày mai)` +
          (res.skippedUnassignedCount > 0 ? `, ${res.skippedUnassignedCount} mục chưa có người làm.` : '.'),
      );
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setGenerating(false);
    }
  };

  const openCreate = () => {
    form.resetFields();
    form.setFieldsValue({
      startDate: dayjs(),
      items: [{ id: 0, title: '', timeOfDay: dayjs().hour(8).minute(0), days: [0, 1, 2, 3, 4, 5, 6] }],
    });
    setEditing('new');
  };

  const openEdit = (p: CarePlan) => {
    form.setFieldsValue({
      title: p.title,
      goals: p.goals,
      startDate: dayjs(p.startDate),
      endDate: p.endDate ? dayjs(p.endDate) : null,
      items: p.items.map((i) => ({
        id: i.id,
        title: i.title,
        instructions: i.instructions,
        timeOfDay: dayjs(`2000-01-01 ${i.timeOfDay}`),
        days: patternToDays(i.daysOfWeek),
        defaultAssigneeId: i.defaultAssigneeId ?? undefined,
      })),
    });
    setEditing(p);
  };

  const savePlan = async () => {
    const v = await form.validateFields();
    setSubmitting(true);
    try {
      const payload: SaveCarePlanForm = {
        residentId,
        title: v.title,
        goals: v.goals,
        startDate: v.startDate.format('YYYY-MM-DD'),
        endDate: v.endDate ? v.endDate.format('YYYY-MM-DD') : null,
        items: (v.items ?? []).map((i: {
          id?: number;
          title: string;
          instructions?: string;
          timeOfDay: dayjs.Dayjs;
          days: number[];
          defaultAssigneeId?: number;
        }) => ({
          id: i.id ?? 0,
          title: i.title,
          instructions: i.instructions,
          timeOfDay: i.timeOfDay.format('HH:mm:00'),
          daysOfWeek: daysToPattern(i.days),
          defaultAssigneeId: i.defaultAssigneeId ?? null,
        })),
      };
      if (editing === 'new') {
        await carePlansApi.create(payload);
      } else if (editing) {
        await carePlansApi.update(editing.id, payload);
      }
      message.success('Đã lưu kế hoạch chăm sóc.');
      setEditing(null);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      {canPlan && (
        <Space style={{ marginBottom: 12 }} wrap>
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            Tạo kế hoạch chăm sóc
          </Button>
          <Button icon={<ThunderboltOutlined />} loading={generating} onClick={generateNow}>
            Sinh việc ngay
          </Button>
        </Space>
      )}

      {plans.length === 0 ? (
        <Typography.Text type="secondary">Chưa có kế hoạch chăm sóc nào.</Typography.Text>
      ) : (
        <Space orientation="vertical" style={{ width: '100%' }} size={12}>
          {plans.map((p) => (
            <Card
              key={p.id}
              size="small"
              title={
                <Space>
                  <span>{p.title}</span>
                  {p.isActive ? <Tag color="green">Đang áp dụng</Tag> : <Tag>Đã ngừng</Tag>}
                </Space>
              }
              extra={
                canPlan && p.isActive && (
                  <Space>
                    <Button size="small" onClick={() => openEdit(p)}>Sửa</Button>
                    <Popconfirm
                      title="Ngừng áp dụng kế hoạch này?"
                      onConfirm={async () => {
                        await carePlansApi.deactivate(p.id);
                        void load();
                      }}
                    >
                      <Button size="small" danger>Ngừng</Button>
                    </Popconfirm>
                  </Space>
                )
              }
            >
              <Typography.Paragraph style={{ marginBottom: 8 }}>
                <b>Hiệu lực:</b> {dateOnly(p.startDate)} – {p.endDate ? dateOnly(p.endDate) : 'Không giới hạn'}
                {p.goals ? ` · Mục tiêu: ${p.goals}` : ''}
              </Typography.Paragraph>
              <Table
                rowKey="id"
                size="small"
                dataSource={p.items}
                pagination={false}
                columns={[
                  { title: 'Giờ', dataIndex: 'timeOfDay', render: hhmm, width: 80 },
                  { title: 'Đầu việc', dataIndex: 'title' },
                  { title: 'Lịch lặp', dataIndex: 'daysOfWeek', render: formatDays, width: 160 },
                  { title: 'Người làm mặc định', dataIndex: 'defaultAssigneeName', render: (v?: string) => v || 'Theo phân công chính' },
                ]}
              />
            </Card>
          ))}
        </Space>
      )}

      <Modal
        open={editing !== null}
        title={editing === 'new' ? 'Tạo kế hoạch chăm sóc' : 'Cập nhật kế hoạch chăm sóc'}
        onOk={savePlan}
        onCancel={() => setEditing(null)}
        confirmLoading={submitting}
        width={720}
        okText="Lưu kế hoạch"
        destroyOnHidden
      >
        <Form form={form} layout="vertical">
          <Form.Item name="title" label="Tiêu đề kế hoạch" rules={[{ required: true, message: 'Nhập tiêu đề' }]}>
            <Input maxLength={200} placeholder="Ví dụ: Chăm sóc sau phẫu thuật / Theo dõi đường huyết" />
          </Form.Item>
          <Form.Item name="goals" label="Mục tiêu chăm sóc">
            <Input.TextArea rows={2} maxLength={1000} />
          </Form.Item>
          <Space wrap>
            <Form.Item name="startDate" label="Từ ngày" rules={[{ required: true }]}>
              <DatePicker format="DD/MM/YYYY" allowClear={false} />
            </Form.Item>
            <Form.Item name="endDate" label="Đến ngày (bỏ trống nếu dài hạn)">
              <DatePicker format="DD/MM/YYYY" />
            </Form.Item>
          </Space>

          <Typography.Title level={5} style={{ marginTop: 8 }}>Các đầu việc lặp lại</Typography.Title>
          <Form.List name="items">
            {(fields, { add, remove }) => (
              <>
                {fields.map(({ key, name, ...rest }) => (
                  <Card
                    key={key}
                    size="small"
                    style={{ marginBottom: 10, background: '#fafafa' }}
                    extra={
                      fields.length > 1 && (
                        <Button size="small" danger icon={<DeleteOutlined />} onClick={() => remove(name)} />
                      )
                    }
                  >
                    <Form.Item {...rest} name={[name, 'id']} hidden><Input /></Form.Item>
                    <Space style={{ display: 'flex', flexWrap: 'wrap' }} align="start">
                      <Form.Item {...rest} name={[name, 'title']} label="Tên việc" rules={[{ required: true }]} style={{ minWidth: 240 }}>
                        <Input maxLength={200} placeholder="Đo đường huyết, tắm..." />
                      </Form.Item>
                      <Form.Item {...rest} name={[name, 'timeOfDay']} label="Giờ" rules={[{ required: true }]}>
                        <TimePicker format="HH:mm" minuteStep={5} />
                      </Form.Item>
                      <Form.Item {...rest} name={[name, 'defaultAssigneeId']} label="Người làm (tùy chọn)" style={{ minWidth: 200 }}>
                        <AccountSelect roles={['Nurse', 'Caregiver']} />
                      </Form.Item>
                    </Space>
                    <Form.Item {...rest} name={[name, 'days']} label="Các thứ áp dụng" rules={[{ required: true, message: 'Chọn ít nhất 1 ngày' }]}>
                      <Checkbox.Group options={DAY_LABELS.map((l, idx) => ({ label: l, value: idx }))} />
                    </Form.Item>
                    <Form.Item {...rest} name={[name, 'instructions']} label="Hướng dẫn" style={{ marginBottom: 0 }}>
                      <Input maxLength={1000} />
                    </Form.Item>
                  </Card>
                ))}
                <Button
                  type="dashed"
                  block
                  icon={<PlusOutlined />}
                  onClick={() => add({ id: 0, title: '', timeOfDay: dayjs().hour(8).minute(0), days: [0, 1, 2, 3, 4, 5, 6] })}
                >
                  Thêm đầu việc
                </Button>
              </>
            )}
          </Form.List>
        </Form>
      </Modal>
    </>
  );
}

// ======================= S3-H4 (UC-56, UC-57): ADL & Bữa ăn =======================
const MEAL_OPTIONS: { value: MealType; label: string }[] = [
  { value: 'Breakfast', label: 'Bữa sáng' },
  { value: 'Lunch', label: 'Bữa trưa' },
  { value: 'Dinner', label: 'Bữa tối' },
  { value: 'Snack', label: 'Bữa phụ' },
];

export const ADL_CATEGORY_OPTIONS: { value: DailyCareCategory; label: string }[] = [
  { value: 'Bathing', label: 'Tắm rửa' },
  { value: 'Toileting', label: 'Vệ sinh' },
  { value: 'Mobility', label: 'Vận động' },
  { value: 'Sleep', label: 'Giấc ngủ' },
  { value: 'Mood', label: 'Tâm trạng' },
  { value: 'Other', label: 'Khác' },
];

export const ADL_LEVEL_OPTIONS: { value: DailyCareLevel; label: string }[] = [
  { value: 'Independent', label: 'Tự làm' },
  { value: 'NeedsHelp', label: 'Cần hỗ trợ' },
  { value: 'Dependent', label: 'Phụ thuộc hoàn toàn' },
  { value: 'Good', label: 'Tốt' },
  { value: 'Fair', label: 'Trung bình' },
  { value: 'Poor', label: 'Kém' },
];

export function MealModal({
  open,
  residentId,
  date,
  meal,
  existing,
  onClose,
  onSaved,
}: {
  open: boolean;
  residentId: number;
  date: string;
  meal: MealType;
  existing?: MealLog;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (open) {
      form.setFieldsValue({
        intakePercent: existing?.intakePercent ?? 100,
        fluidMl: existing?.fluidMl ?? 200,
        note: existing?.note ?? '',
        shareWithFamily: existing?.shareWithFamily ?? true,
      });
    }
  }, [open, existing, form]);

  const handleOk = async () => {
    const v = await form.validateFields();
    setSaving(true);
    try {
      await dailyLogsApi.upsertMeal(residentId, {
        mealDate: date,
        meal,
        intakePercent: v.intakePercent,
        fluidMl: v.fluidMl,
        note: v.note,
        shareWithFamily: v.shareWithFamily,
      });
      message.success('Đã lưu ghi nhận bữa ăn.');
      onSaved();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  const label = MEAL_OPTIONS.find((m) => m.value === meal)?.label ?? meal;

  return (
    <Modal open={open} title={`Ghi nhận ${label} (${dateOnly(date)})`} onOk={handleOk} onCancel={onClose} confirmLoading={saving} okText="Lưu" destroyOnHidden>
      <Form form={form} layout="vertical">
        <Form.Item name="intakePercent" label="Lượng ăn được (%)" rules={[{ required: true }]}>
          <Radio.Group optionType="button" buttonStyle="solid">
            {[0, 25, 50, 75, 100].map((p) => (
              <Radio.Button key={p} value={p}>{p}%</Radio.Button>
            ))}
          </Radio.Group>
        </Form.Item>
        <Form.Item name="fluidMl" label="Lượng nước uống (ml)">
          <InputNumber min={0} max={5000} step={50} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item name="note" label="Ghi chú">
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>
        <Form.Item name="shareWithFamily" valuePropName="checked">
          <Checkbox>Chia sẻ với gia đình</Checkbox>
        </Form.Item>
      </Form>
    </Modal>
  );
}

export function AdlModal({
  open,
  residentId,
  date,
  onClose,
  onSaved,
}: {
  open: boolean;
  residentId: number;
  date: string;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (open) {
      form.resetFields();
      form.setFieldsValue({ category: 'Bathing', level: 'NeedsHelp', shareWithFamily: true });
    }
  }, [open, form]);

  const handleOk = async () => {
    const v = await form.validateFields();
    setSaving(true);
    try {
      await dailyLogsApi.addLog(residentId, {
        logDate: date,
        category: v.category,
        level: v.level,
        note: v.note,
        shareWithFamily: v.shareWithFamily,
      });
      message.success('Đã ghi nhận sinh hoạt ADL.');
      onSaved();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal open={open} title={`Ghi nhận ADL (${dateOnly(date)})`} onOk={handleOk} onCancel={onClose} confirmLoading={saving} okText="Lưu" destroyOnHidden>
      <Form form={form} layout="vertical">
        <Form.Item name="category" label="Hạng mục" rules={[{ required: true }]}>
          <Select options={ADL_CATEGORY_OPTIONS} />
        </Form.Item>
        <Form.Item name="level" label="Mức độ" rules={[{ required: true }]}>
          <Select options={ADL_LEVEL_OPTIONS} />
        </Form.Item>
        <Form.Item name="note" label="Ghi chú">
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>
        <Form.Item name="shareWithFamily" valuePropName="checked">
          <Checkbox>Chia sẻ với gia đình</Checkbox>
        </Form.Item>
      </Form>
    </Modal>
  );
}

export function DailyLogsTab({ residentId, active }: { residentId: number; active: boolean }) {
  const [date, setDate] = useState<dayjs.Dayjs>(dayjs());
  const [logs, setLogs] = useState<DailyCareLog[]>([]);
  const [meals, setMeals] = useState<MealLog[]>([]);
  const [mealEditing, setMealEditing] = useState<MealType | null>(null);
  const [adlOpen, setAdlOpen] = useState(false);

  const dateStr = date.format('YYYY-MM-DD');

  const load = useCallback(() => {
    dailyLogsApi.listLogs(residentId, dateStr).then(setLogs).catch(() => undefined);
    dailyLogsApi.listMeals(residentId, dateStr).then(setMeals).catch(() => undefined);
  }, [residentId, dateStr]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <>
      <Space style={{ marginBottom: 16 }} wrap>
        <DatePicker value={date} onChange={(d) => d && setDate(d)} format="DD/MM/YYYY" allowClear={false} />
        {active && (
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setAdlOpen(true)}>
            Ghi nhận ADL
          </Button>
        )}
      </Space>

      <Typography.Title level={5}>Bữa ăn &amp; lượng nước</Typography.Title>
      <Space style={{ marginBottom: 16 }} wrap>
        {MEAL_OPTIONS.map((m) => {
          const item = meals.find((x) => x.meal === m.value);
          return (
            <Button
              key={m.value}
              disabled={!active}
              onClick={() => setMealEditing(m.value)}
            >
              {m.label}: {item ? `${item.intakePercent}%${item.fluidMl ? ` · ${item.fluidMl}ml` : ''}` : 'Chưa ghi'}
            </Button>
          );
        })}
      </Space>

      <Typography.Title level={5}>Nhật ký sinh hoạt (ADL)</Typography.Title>
      <Table
        rowKey="id"
        size="small"
        dataSource={logs}
        pagination={false}
        columns={[
          { title: 'Lúc', dataIndex: 'recordedAt', render: dateTime },
          { title: 'Hạng mục', dataIndex: 'category', render: (v: string) => <StatusTag kind="adlCategory" value={v} /> },
          { title: 'Mức độ', dataIndex: 'level', render: (v: string) => <StatusTag kind="adlLevel" value={v} /> },
          { title: 'Ghi chú', dataIndex: 'note' },
          { title: 'Người ghi', dataIndex: 'recordedByName' },
        ]}
      />

      {mealEditing && (
        <MealModal
          open
          residentId={residentId}
          date={dateStr}
          meal={mealEditing}
          existing={meals.find((m) => m.meal === mealEditing)}
          onClose={() => setMealEditing(null)}
          onSaved={() => {
            setMealEditing(null);
            void load();
          }}
        />
      )}
      <AdlModal
        open={adlOpen}
        residentId={residentId}
        date={dateStr}
        onClose={() => setAdlOpen(false)}
        onSaved={() => {
          setAdlOpen(false);
          void load();
        }}
      />
    </>
  );
}
