import { apiClient } from './client';
import { odataList, type ODataQuery } from './odata';
import type {
  Checkout, InvoiceDetail, InvoiceListItem, PaymentStatus, PaymentTransaction, ServicePackage, Subscription, WebhookLog,
} from '../types';

export interface PackageForm {
  name: string;
  monthlyPrice: number;
  description?: string | null;
  isActive: boolean;
}

export const billingApi = {
  packages: (activeOnly = false) =>
    apiClient.get<ServicePackage[]>('/api/service-packages', { params: { activeOnly } }).then((r) => r.data),
  createPackage: (body: PackageForm) => apiClient.post<ServicePackage>('/api/service-packages', body).then((r) => r.data),
  updatePackage: (id: number, body: PackageForm) => apiClient.put<ServicePackage>(`/api/service-packages/${id}`, body).then((r) => r.data),

  subscriptions: (residentId?: number) =>
    apiClient.get<Subscription[]>('/api/subscriptions', { params: { residentId } }).then((r) => r.data),
  subscribe: (body: { residentId: number; servicePackageId: number; startDate: string; endDate?: string | null }) =>
    apiClient.post<Subscription>('/api/subscriptions', body).then((r) => r.data),
  endSubscription: (id: number) => apiClient.post(`/api/subscriptions/${id}/end`),

  invoices: (query: ODataQuery) => odataList<InvoiceListItem>('Invoices', query),
  invoice: (id: number) => apiClient.get<InvoiceDetail>(`/api/invoices/${id}`).then((r) => r.data),
  createInvoice: (body: { residentId: number; billingPeriod: string; dueDate: string }) =>
    apiClient.post<InvoiceDetail>('/api/invoices', body).then((r) => r.data),
  addItem: (id: number, body: { description: string; quantity: number; unitPrice: number }) =>
    apiClient.post<InvoiceDetail>(`/api/invoices/${id}/items`, body).then((r) => r.data),
  issue: (id: number) => apiClient.post<InvoiceDetail>(`/api/invoices/${id}/issue`).then((r) => r.data),
  recordPayment: (id: number, body: { amount: number; method: 'Cash' | 'BankTransfer'; reference: string }) =>
    apiClient.post<InvoiceDetail>(`/api/invoices/${id}/payments`, body).then((r) => r.data),
  cancel: (id: number) => apiClient.post(`/api/invoices/${id}/cancel`),

  // ---- PayOS ----
  checkout: (invoiceId: number) => apiClient.post<Checkout>(`/api/invoices/${invoiceId}/payos/checkout`).then((r) => r.data),
  paymentStatus: (transactionId: number) =>
    apiClient.get<PaymentStatus>(`/api/payments/transactions/${transactionId}`).then((r) => r.data),
  syncPayment: (transactionId: number) =>
    apiClient.post<PaymentStatus>(`/api/payments/transactions/${transactionId}/sync`).then((r) => r.data),
  transactions: (query: ODataQuery) => odataList<PaymentTransaction>('PaymentTransactions', query),
  webhookLogs: () => apiClient.get<WebhookLog[]>('/api/payments/webhook-logs').then((r) => r.data),
};
