import { apiClient } from './client';
import { downloadFile } from './reports';
import type {
  BatchInvoiceResult, ExtraService, ExtraServiceForm, InvoiceReminder, ReminderRunResult, ServiceUsage, ServiceUsageForm,
} from '../types/operations';

/** S3-V5 — UC-85, 86 dịch vụ phát sinh; UC-98 hóa đơn hàng loạt; UC-88 PDF; UC-97 nhắc nợ. */
export const billingExtrasApi = {
  services: (activeOnly = false) => apiClient.get<ExtraService[]>('/api/extra-services', { params: { activeOnly } }).then((r) => r.data),
  createService: (body: ExtraServiceForm) => apiClient.post<ExtraService>('/api/extra-services', body).then((r) => r.data),
  updateService: (id: number, body: ExtraServiceForm) => apiClient.put<ExtraService>(`/api/extra-services/${id}`, body).then((r) => r.data),

  /** Mọi cư dân mình được xem; mặc định từ đầu tháng tới hôm nay. */
  usages: (from?: string, to?: string) =>
    apiClient.get<ServiceUsage[]>('/api/service-usages', { params: { from, to } }).then((r) => r.data),
  residentUsages: (residentId: number, from?: string, to?: string) =>
    apiClient.get<ServiceUsage[]>(`/api/residents/${residentId}/service-usages`, { params: { from, to } }).then((r) => r.data),
  recordUsage: (residentId: number, body: ServiceUsageForm) =>
    apiClient.post<ServiceUsage>(`/api/residents/${residentId}/service-usages`, body).then((r) => r.data),
  deleteUsage: (id: number) => apiClient.delete(`/api/service-usages/${id}`),

  batch: (billingPeriod: string, dueDate: string) =>
    apiClient.post<BatchInvoiceResult>('/api/invoices/batch', { billingPeriod, dueDate }).then((r) => r.data),
  runReminders: () => apiClient.post<ReminderRunResult>('/api/invoices/reminders/run').then((r) => r.data),
  reminders: (invoiceId: number) => apiClient.get<InvoiceReminder[]>(`/api/invoices/${invoiceId}/reminders`).then((r) => r.data),
  downloadPdf: (invoiceId: number, number: string) => downloadFile(`/api/invoices/${invoiceId}/pdf`, `${number}.pdf`),
};
