import { apiClient } from './client';
import type { CareAssignment, CareAssignmentForm, CopyWeekResult, Shift, ShiftAssignment } from '../types/operations';

/** S3-V2 — UC-44, 45, 46. Xếp/bỏ/chép ca: Admin; phân công phụ trách: Admin + Nurse. */
export const staffingApi = {
  shifts: () => apiClient.get<Shift[]>('/api/shifts').then((r) => r.data),
  assignments: (from: string, to: string) =>
    apiClient.get<ShiftAssignment[]>('/api/shift-assignments', { params: { from, to } }).then((r) => r.data),
  mine: (days = 14) => apiClient.get<ShiftAssignment[]>('/api/shift-assignments/mine', { params: { days } }).then((r) => r.data),
  assign: (body: { accountId: number; shiftId: number; workDate: string; note?: string }) =>
    apiClient.post<ShiftAssignment>('/api/shift-assignments', body).then((r) => r.data),
  unassign: (id: number) => apiClient.delete(`/api/shift-assignments/${id}`),
  copyWeek: (fromWeekStart: string, toWeekStart: string) =>
    apiClient.post<CopyWeekResult>('/api/shift-assignments/copy-week', { fromWeekStart, toWeekStart }).then((r) => r.data),

  careAssignments: (params: { residentId?: number; accountId?: number; activeOnly?: boolean } = {}) =>
    apiClient.get<CareAssignment[]>('/api/care-assignments', { params }).then((r) => r.data),
  addCareAssignment: (body: CareAssignmentForm) => apiClient.post<CareAssignment>('/api/care-assignments', body).then((r) => r.data),
  endCareAssignment: (id: number) => apiClient.post<CareAssignment>(`/api/care-assignments/${id}/end`).then((r) => r.data),
};
