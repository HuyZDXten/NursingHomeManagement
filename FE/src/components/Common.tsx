import { useEffect, useState } from 'react';
import { Select, Tag, Typography } from 'antd';
import type { ReactNode } from 'react';
import { accountsApi } from '../api/accounts';
import { residentsApi } from '../api/residents';
import { statusMeta } from '../utils/status';
import type { LookupItem, ResidentListItem, Role } from '../types';

/** Tag trạng thái có nhãn tiếng Việt, ví dụ <StatusTag kind="invoice" value="Paid" />. */
export function StatusTag({ kind, value }: { kind: string; value: string }) {
  const meta = statusMeta(kind, value);
  return <Tag color={meta.color}>{meta.label}</Tag>;
}

export function PageTitle({ title, extra }: { title: ReactNode; extra?: ReactNode }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap', marginBottom: 16 }}>
      <Typography.Title level={3} style={{ margin: 0 }}>{title}</Typography.Title>
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>{extra}</div>
    </div>
  );
}

/** Chọn cư dân đang lưu trú (tìm theo mã hoặc tên). */
export function ResidentSelect({ value, onChange, disabled }: { value?: number; onChange?: (id: number) => void; disabled?: boolean }) {
  const [options, setOptions] = useState<ResidentListItem[]>([]);
  useEffect(() => {
    residentsApi.list({ filter: ["status eq 'Active'"], orderBy: 'code', top: 200 }).then((page) => setOptions(page.items)).catch(() => undefined);
  }, []);
  return (
    <Select
      showSearch
      disabled={disabled}
      value={value}
      onChange={onChange}
      placeholder="Chọn cư dân"
      optionFilterProp="label"
      options={options.map((r) => ({ value: r.id, label: `${r.code} — ${r.fullName}` }))}
    />
  );
}

/** Chọn tài khoản đang hoạt động theo role (gọi /api/accounts/lookup). */
export function AccountSelect({ roles, value, onChange }: { roles: Role[]; value?: number; onChange?: (id: number) => void }) {
  const [options, setOptions] = useState<LookupItem[]>([]);
  const key = roles.join(',');
  useEffect(() => {
    accountsApi.lookup(key.split(',') as Role[]).then(setOptions).catch(() => undefined);
  }, [key]);
  return (
    <Select
      showSearch
      value={value}
      onChange={onChange}
      placeholder="Chọn tài khoản"
      optionFilterProp="label"
      options={options.map((a) => ({ value: a.id, label: `${a.name} (${a.extra ?? ''})` }))}
    />
  );
}
