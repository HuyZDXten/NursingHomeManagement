import { apiClient } from './client';
import { downloadFile } from './reports';
import type { ResidentDetail } from '../types';
import type {
  ContractForm, DepositSummary, DocumentKind, LeaveKind, ReadmitForm, ResidentContract, ResidentDocument, ResidentLeave, ResidentStay,
} from '../types/operations';

/** BL-V2 hồ sơ lưu trú: giấy tờ, hợp đồng, tạm vắng, tái nhập viện, tiền cọc. */
export const residentRecordsApi = {
  documents: (residentId: number) => apiClient.get<ResidentDocument[]>(`/api/residents/${residentId}/documents`).then((r) => r.data),
  uploadDocument: (residentId: number, kind: DocumentKind, title: string, file: File) => {
    const form = new FormData();
    form.append('kind', kind);
    form.append('title', title);
    form.append('file', file);
    return apiClient.post<ResidentDocument>(`/api/residents/${residentId}/documents`, form).then((r) => r.data);
  },
  downloadDocument: (residentId: number, doc: ResidentDocument) =>
    downloadFile(`/api/residents/${residentId}/documents/${doc.id}/file`, doc.fileName),
  deleteDocument: (residentId: number, id: number) => apiClient.delete(`/api/residents/${residentId}/documents/${id}`),

  contracts: (residentId: number) => apiClient.get<ResidentContract[]>(`/api/residents/${residentId}/contracts`).then((r) => r.data),
  createContract: (residentId: number, body: ContractForm, file?: File) => {
    const form = new FormData();
    Object.entries(body).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') form.append(key, String(value));
    });
    if (file) form.append('file', file);
    return apiClient.post<ResidentContract>(`/api/residents/${residentId}/contracts`, form).then((r) => r.data);
  },
  endContract: (residentId: number, id: number, endDate?: string) =>
    apiClient.post<ResidentContract>(`/api/residents/${residentId}/contracts/${id}/end`, { endDate }).then((r) => r.data),
  downloadContract: (residentId: number, contract: ResidentContract) =>
    downloadFile(`/api/residents/${residentId}/contracts/${contract.id}/file`, contract.fileName ?? `${contract.number}.pdf`),

  openLeaves: () => apiClient.get<ResidentLeave[]>('/api/resident-leaves/open').then((r) => r.data),
  leaves: (residentId: number) => apiClient.get<ResidentLeave[]>(`/api/residents/${residentId}/leaves`).then((r) => r.data),
  startLeave: (residentId: number, body: { kind: LeaveKind; expectedReturnOn?: string | null; reason: string }) =>
    apiClient.post<ResidentLeave>(`/api/residents/${residentId}/leaves`, body).then((r) => r.data),
  returnFromLeave: (residentId: number, leaveId: number) =>
    apiClient.post<ResidentLeave>(`/api/residents/${residentId}/leaves/${leaveId}/return`, {}).then((r) => r.data),

  stays: (residentId: number) => apiClient.get<ResidentStay[]>(`/api/residents/${residentId}/stays`).then((r) => r.data),
  readmit: (residentId: number, body: ReadmitForm) =>
    apiClient.post<ResidentDetail>(`/api/residents/${residentId}/readmit`, body).then((r) => r.data),

  deposits: (residentId: number) => apiClient.get<DepositSummary>(`/api/residents/${residentId}/deposits`).then((r) => r.data),
  createDeposit: (residentId: number, body: { amount: number; method: 'Cash' | 'BankTransfer'; reference?: string }) =>
    apiClient.post<DepositSummary>(`/api/residents/${residentId}/deposits`, body).then((r) => r.data),
  refundDeposit: (depositId: number, body: { amount: number; note?: string }) =>
    apiClient.post<DepositSummary>(`/api/deposits/${depositId}/refund`, body).then((r) => r.data),
};
