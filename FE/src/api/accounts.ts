import { apiClient } from './client';
import { odataList, type ODataQuery } from './odata';
import type { Account, LookupItem, Role } from '../types';

export interface AccountForm {
  email?: string;
  fullName: string;
  phone?: string | null;
  role: Role;
  isActive?: boolean;
  password?: string;
}

export const accountsApi = {
  list: (query: ODataQuery) => odataList<Account>('Accounts', query),
  create: (body: AccountForm) => apiClient.post<Account>('/api/accounts', body).then((r) => r.data),
  update: (id: number, body: AccountForm) => apiClient.put<Account>(`/api/accounts/${id}`, body).then((r) => r.data),
  resetPassword: (id: number, newPassword: string) => apiClient.post(`/api/accounts/${id}/reset-password`, { newPassword }),
  /** Danh sách chọn nhanh theo role, vd. ['Caregiver','Nurse']. */
  lookup: (roles: Role[]) => apiClient.get<LookupItem[]>('/api/accounts/lookup', { params: { roles: roles.join(',') } }).then((r) => r.data),
};
