import { apiClient } from './client';
import type { VisitBooking, VisitSlot, VisitStatus } from '../types/care';

export interface BookVisitForm {
  residentId: number;
  visitDate: string;
  slotStart: string;
  guestCount: number;
  guestNames?: string;
  note?: string;
}

export const visitsApi = {
  slots: (date: string) =>
    apiClient.get<VisitSlot[]>('/api/visits/slots', { params: { date } }).then((r) => r.data),
  book: (body: BookVisitForm) =>
    apiClient.post<VisitBooking>('/api/visits', body).then((r) => r.data),
  mine: () =>
    apiClient.get<VisitBooking[]>('/api/visits/mine').then((r) => r.data),
  cancel: (id: number) =>
    apiClient.post<VisitBooking>(`/api/visits/${id}/cancel`).then((r) => r.data),
  listByDate: (date: string, status?: VisitStatus) =>
    apiClient.get<VisitBooking[]>('/api/visits', { params: { date, status } }).then((r) => r.data),
  approve: (id: number, note?: string) =>
    apiClient.post<VisitBooking>(`/api/visits/${id}/approve`, { note }).then((r) => r.data),
  reject: (id: number, note: string) =>
    apiClient.post<VisitBooking>(`/api/visits/${id}/reject`, { note }).then((r) => r.data),
  checkIn: (id: number) =>
    apiClient.post<VisitBooking>(`/api/visits/${id}/check-in`).then((r) => r.data),
  complete: (id: number) =>
    apiClient.post<VisitBooking>(`/api/visits/${id}/complete`).then((r) => r.data),
  noShow: (id: number) =>
    apiClient.post<VisitBooking>(`/api/visits/${id}/no-show`).then((r) => r.data),
};
