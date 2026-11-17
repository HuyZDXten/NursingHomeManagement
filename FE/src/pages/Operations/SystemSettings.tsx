import { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert, App, Button, Card, Form, Input, InputNumber, Modal, Popconfirm, Segmented, Space, Switch, Table, Tabs, Tag, Typography } from 'antd';
import { PlusOutlined, UndoOutlined } from '@ant-design/icons';
import { extractErrorMessage } from '../../api/client';
import { systemApi } from '../../api/system';
import { PageTitle } from '../../components/Common';
import { LOOKUP_CATEGORY_LABEL, type Lookup, type LookupCategory, type LookupForm, type Setting } from '../../types/system';

/** UC-129: cấu hình cơ sở. Chỉ gửi các khóa Admin đã đổi; áp dụng ngay, không cần khởi động lại. */
function FacilitySettings() {
  const { message } = App.useApp();
  const [settings, setSettings] = useState<Setting[]>([]);
  const [draft, setDraft] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  const apply = useCallback((rows: Setting[]) => {
    setSettings(rows);
    setDraft({});
  }, []);
  const load = useCallback(() => systemApi.settings().then(apply).catch((e) => message.error(extractErrorMessage(e))), [apply, message]);
  useEffect(() => {
    void load();
  }, [load]);

  const groups = useMemo(() => [...new Set(settings.map((s) => s.group))], [settings]);
  const changed = settings.filter((s) => draft[s.key] !== undefined && draft[s.key] !== s.value);
  const valueOf = (s: Setting) => draft[s.key] ?? s.value;

  const save = async () => {
    setSaving(true);
    try {
      apply(await systemApi.saveSettings(changed.map((s) => ({ key: s.key, value: draft[s.key] }))));
      message.success('Đã lưu cấu hình, áp dụng ngay.');
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  const reset = async (s: Setting) => {
    try {
      apply(await systemApi.resetSetting(s.key));
      message.success('Đã đặt lại về mặc định.');
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <>
      <Alert type="info" showIcon style={{ marginBottom: 16 }}
        title="Giá trị ở đây ghi đè cấu hình mặc định của hệ thống. Chỉ các mục dưới đây sửa được; khóa bảo mật (mật khẩu, khóa API) chỉ đặt ở máy chủ." />
      {groups.map((group) => (
        <Card key={group} title={group} size="small" style={{ marginBottom: 16 }}>
          {settings.filter((s) => s.group === group).map((s) => (
            <div key={s.key} style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap', padding: '6px 0' }}>
              <div style={{ width: 300, maxWidth: '100%' }}>{s.label}</div>
              {s.kind === 'Text'
                ? <Input style={{ width: 300, maxWidth: '100%' }} maxLength={300} value={valueOf(s)} onChange={(e) => setDraft({ ...draft, [s.key]: e.target.value })} />
                : <InputNumber style={{ width: 140 }} min={s.min ?? undefined} max={s.max ?? undefined} step={s.kind === 'Decimal' ? 0.1 : 1}
                    value={Number(valueOf(s))} onChange={(v) => setDraft({ ...draft, [s.key]: v == null ? '' : String(v) })} />}
              {s.isOverridden && <Tag color="blue">Đã chỉnh</Tag>}
              <Typography.Text type="secondary">Mặc định: {s.defaultValue || '(trống)'}</Typography.Text>
              {s.isOverridden && (
                <Popconfirm title="Đặt lại về giá trị mặc định?" onConfirm={() => reset(s)}>
                  <Button size="small" icon={<UndoOutlined />}>Đặt lại</Button>
                </Popconfirm>
              )}
            </div>
          ))}
        </Card>
      ))}
      <Button type="primary" loading={saving} disabled={changed.length === 0} onClick={save}>
        Lưu cấu hình{changed.length ? ` (${changed.length} thay đổi)` : ''}
      </Button>
    </>
  );
}

/** UC-130: danh mục dùng chung. Tên được lưu thẳng vào bản ghi dùng nó, nên sửa/ẩn/xóa mục không làm hỏng dữ liệu cũ. */
function Lookups() {
  const { message } = App.useApp();
  const [category, setCategory] = useState<LookupCategory>('Relationship');
  const [rows, setRows] = useState<Lookup[]>([]);
  const [editing, setEditing] = useState<Lookup | 'new' | null>(null);
  const [form] = Form.useForm<LookupForm>();

  const load = useCallback(() => systemApi.lookups(category, true).then(setRows).catch((e) => message.error(extractErrorMessage(e))), [category, message]);
  useEffect(() => {
    void load();
  }, [load]);

  const open = (target: Lookup | 'new') => {
    setEditing(target);
    form.setFieldsValue(target === 'new' ? { code: '', name: '', sortOrder: (rows.at(-1)?.sortOrder ?? 0) + 1, isActive: true } : target);
  };

  const save = async () => {
    const values = await form.validateFields();
    try {
      if (editing === 'new') await systemApi.createLookup({ ...values, category });
      else if (editing) await systemApi.updateLookup(editing.id, { ...values, category });
      message.success('Đã lưu mục danh mục.');
      setEditing(null);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  const remove = async (row: Lookup) => {
    try {
      await systemApi.deleteLookup(row.id);
      message.success('Đã xóa mục danh mục.');
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <>
      <Space style={{ marginBottom: 16 }} wrap>
        <Segmented<LookupCategory> value={category} onChange={setCategory}
          options={(Object.keys(LOOKUP_CATEGORY_LABEL) as LookupCategory[]).map((c) => ({ value: c, label: LOOKUP_CATEGORY_LABEL[c] }))} />
        <Button type="primary" icon={<PlusOutlined />} onClick={() => open('new')}>Thêm mục</Button>
      </Space>
      <Table<Lookup> rowKey="id" size="small" dataSource={rows} pagination={false} scroll={{ x: 600 }} columns={[
        { title: 'Thứ tự', dataIndex: 'sortOrder', width: 90 },
        { title: 'Mã', dataIndex: 'code', width: 200 },
        { title: 'Tên hiển thị', dataIndex: 'name' },
        { title: 'Trạng thái', width: 110, render: (_, r) => (r.isActive ? <Tag color="green">Đang dùng</Tag> : <Tag>Đã ẩn</Tag>) },
        {
          title: '', width: 150, render: (_, r) => (
            <Space>
              <Button size="small" onClick={() => open(r)}>Sửa</Button>
              <Popconfirm title="Xóa mục này? Dữ liệu cũ đã dùng tên này vẫn giữ nguyên." onConfirm={() => remove(r)}><Button size="small" danger>Xóa</Button></Popconfirm>
            </Space>
          ),
        },
      ]} />
      <Modal open={!!editing} title={editing === 'new' ? `Thêm mục: ${LOOKUP_CATEGORY_LABEL[category]}` : 'Sửa mục danh mục'} okText="Lưu" cancelText="Hủy"
        onOk={save} onCancel={() => setEditing(null)} destroyOnHidden>
        <Form form={form} layout="vertical">
          <Form.Item name="code" label="Mã (không dấu, viết liền, ví dụ CON_TRAI)" rules={[{ required: true, max: 50 }]}><Input /></Form.Item>
          <Form.Item name="name" label="Tên hiển thị" rules={[{ required: true, max: 100 }]}><Input /></Form.Item>
          <Form.Item name="sortOrder" label="Thứ tự" rules={[{ required: true }]}><InputNumber min={0} max={9999} /></Form.Item>
          <Form.Item name="isActive" label="Đang dùng" valuePropName="checked"><Switch /></Form.Item>
        </Form>
      </Modal>
    </>
  );
}

/** BL-V5, UC-129/130 (Admin): cấu hình cơ sở và danh mục dùng chung. */
export default function SystemSettings() {
  return (
    <>
      <PageTitle title="Cấu hình & danh mục" />
      <Tabs items={[
        { key: 'settings', label: 'Cấu hình cơ sở', children: <FacilitySettings /> },
        { key: 'lookups', label: 'Danh mục', children: <Lookups /> },
      ]} />
    </>
  );
}
