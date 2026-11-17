import { apiClient } from './client';
import { downloadFile } from './reports';
import type {
  AttendanceRecord, CertificateAlertResult, CertificateForm, MyShift, StaffCertificate, StaffHome, StaffPerformanceReport,
  StaffProfile, StaffProfileForm, StaffProfileSummary,
} from '../types/operations';

/** BL-V4 nhân sự: hồ sơ & chứng chỉ, chấm công, hiệu suất, trang chủ cá nhân. */
export const staffHrApi = {
  profiles: () => apiClient.get<StaffProfileSummary[]>('/api/staff-profiles').then((r) => r.data),
  profile: (accountId: number) => apiClient.get<StaffProfile>(`/api/staff-profiles/${accountId}`).then((r) => r.data),
  saveProfile: (accountId: number, body: StaffProfileForm) =>
    apiClient.put<StaffProfile>(`/api/staff-profiles/${accountId}`, body).then((r) => r.data),

  addCertificate: (accountId: number, body: CertificateForm) =>
    apiClient.post<StaffCertificate>(`/api/staff-profiles/${accountId}/certificates`, body).then((r) => r.data),
  updateCertificate: (id: number, body: CertificateForm) =>
    apiClient.put<StaffCertificate>(`/api/staff-certificates/${id}`, body).then((r) => r.data),
  deleteCertificate: (id: number) => apiClient.delete(`/api/staff-certificates/${id}`),
  expiring: () => apiClient.get<StaffCertificate[]>('/api/staff-certificates/expiring').then((r) => r.data),
  runAlerts: () => apiClient.post<CertificateAlertResult>('/api/staff-certificates/alerts/run').then((r) => r.data),

  myShifts: () => apiClient.get<MyShift[]>('/api/attendance/mine').then((r) => r.data),
  checkIn: (shiftAssignmentId: number) => apiClient.post<MyShift>('/api/attendance/check-in', { shiftAssignmentId }).then((r) => r.data),
  checkOut: (shiftAssignmentId: number) => apiClient.post<MyShift>('/api/attendance/check-out', { shiftAssignmentId }).then((r) => r.data),
  attendance: (from: string, to: string, accountId?: number) =>
    apiClient.get<AttendanceRecord[]>('/api/attendance', { params: { from, to, accountId } }).then((r) => r.data),
  editAttendance: (shiftAssignmentId: number, body: { checkInAt?: string; checkOutAt?: string | null; reason: string }) =>
    apiClient.put<AttendanceRecord>(`/api/attendance/${shiftAssignmentId}`, body).then((r) => r.data),

  performance: (from: string, to: string) =>
    apiClient.get<StaffPerformanceReport>('/api/reports/staff-performance', { params: { from, to } }).then((r) => r.data),
  exportPerformance: (from: string, to: string) =>
    downloadFile('/api/reports/staff-performance/export', `hieu-suat-nhan-vien-${from.replaceAll('-', '')}-${to.replaceAll('-', '')}.xlsx`, { from, to }),

  home: () => apiClient.get<StaffHome>('/api/staff-home').then((r) => r.data),
};
