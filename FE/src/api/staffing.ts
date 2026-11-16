import { apiClient } from './client';
import type {
  CareAssignment, CareAssignmentForm, CopyWeekResult, Handover, HandoverAiDraft, HandoverDraft, HandoverForm, LeaveApproval, LeaveRequest,
  LeaveRequestForm, LeaveStatus, Shift, ShiftAssignment,
} from '../types/operations';

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

/** S3-V4 — UC-47 nghỉ phép/đổi ca (Admin duyệt), UC-49 bàn giao ca. */
export const handoverApi = {
  leaveRequests: (status?: LeaveStatus) =>
    apiClient.get<LeaveRequest[]>('/api/leave-requests', { params: { status } }).then((r) => r.data),
  createLeave: (body: LeaveRequestForm) => apiClient.post<LeaveRequest>('/api/leave-requests', body).then((r) => r.data),
  swapCandidates: (shiftAssignmentId: number) =>
    apiClient.get<ShiftAssignment[]>('/api/leave-requests/swap-candidates', { params: { shiftAssignmentId } }).then((r) => r.data),
  approve: (id: number, note?: string) =>
    apiClient.post<LeaveApproval>(`/api/leave-requests/${id}/approve`, { note }).then((r) => r.data),
  reject: (id: number, note?: string) => apiClient.post<LeaveRequest>(`/api/leave-requests/${id}/reject`, { note }).then((r) => r.data),
  cancel: (id: number) => apiClient.post<LeaveRequest>(`/api/leave-requests/${id}/cancel`).then((r) => r.data),

  draft: (shiftId: number, date: string) =>
    apiClient.get<HandoverDraft>('/api/handovers/draft', { params: { shiftId, date } }).then((r) => r.data),
  list: (date: string) => apiClient.get<Handover[]>('/api/handovers', { params: { date } }).then((r) => r.data),
  inbox: () => apiClient.get<Handover[]>('/api/handovers/inbox').then((r) => r.data),
  create: (body: HandoverForm) => apiClient.post<Handover>('/api/handovers', body).then((r) => r.data),
  acknowledge: (id: number) => apiClient.post<Handover>(`/api/handovers/${id}/acknowledge`).then((r) => r.data),
  /** S4-V2 (AI-04): nháp tóm tắt do AI soạn, không tự lưu. */
  aiDraft: (shiftId: number, date: string) =>
    apiClient.post<HandoverAiDraft>('/api/handovers/ai-draft', { shiftId, date }).then((r) => r.data),
};
