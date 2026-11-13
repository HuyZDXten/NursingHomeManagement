import { apiClient } from './client';
import type { ODataPage } from '../types';

export interface ODataQuery {
  filter?: string[];
  orderBy?: string;
  top?: number;
  skip?: number;
  /** Tham số thường đi kèm (vd. date, mine của /odata/CareTasks). */
  params?: Record<string, string | number | boolean | undefined>;
}

/** Thoát dấu nháy trong chuỗi OData: O'Brien -> O''Brien. */
export const odataString = (value: string) => `'${value.replace(/'/g, "''")}'`;

/** contains không phân biệt hoa thường trên nhiều trường. */
export function containsAny(fields: string[], keyword: string): string | undefined {
  const k = keyword.trim().toLowerCase();
  if (!k) return undefined;
  return `(${fields.map((f) => `contains(tolower(${f}),${odataString(k)})`).join(' or ')})`;
}

/** Gọi một entity set OData với $count=true, trả về { items, total } cho bảng phân trang. */
export async function odataList<T>(entitySet: string, query: ODataQuery = {}): Promise<ODataPage<T>> {
  const params: Record<string, string | number | boolean> = { $count: true };
  const filters = (query.filter ?? []).filter(Boolean);
  if (filters.length) params.$filter = filters.join(' and ');
  if (query.orderBy) params.$orderby = query.orderBy;
  if (query.top !== undefined) params.$top = query.top;
  if (query.skip !== undefined) params.$skip = query.skip;
  Object.entries(query.params ?? {}).forEach(([k, v]) => {
    if (v !== undefined && v !== '') params[k] = v;
  });
  const { data } = await apiClient.get<{ value: T[]; '@odata.count'?: number }>(`/odata/${entitySet}`, { params });
  return { items: data.value, total: data['@odata.count'] ?? data.value.length };
}
