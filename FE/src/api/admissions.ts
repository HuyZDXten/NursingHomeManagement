import { apiClient } from './client';
import { odataList, type ODataQuery } from './odata';
import type {
  Inquiry, InquiryForm, PublicFormResult, TourBooking, TourBookingForm, TourSlotAvailability, UpdateInquiryForm,
} from '../types/operations';

export type TourAction = 'confirm' | 'cancel' | 'done' | 'no-show';

/** S3-V1 — UC-17, 18, 21, 22. Ba hàm đầu không cần đăng nhập (form công khai, giới hạn 5 lần/phút/IP). */
export const admissionsApi = {
  createInquiry: (body: InquiryForm) => apiClient.post<PublicFormResult>('/api/public/inquiries', body).then((r) => r.data),
  tourSlots: (from: string, to: string) =>
    apiClient.get<TourSlotAvailability[]>('/api/public/tour-slots', { params: { from, to } }).then((r) => r.data),
  bookTour: (body: TourBookingForm) => apiClient.post<PublicFormResult>('/api/public/tour-bookings', body).then((r) => r.data),

  inquiries: (query: ODataQuery) => odataList<Inquiry>('Inquiries', query),
  updateInquiry: (id: number, body: UpdateInquiryForm) => apiClient.put<Inquiry>(`/api/inquiries/${id}`, body).then((r) => r.data),
  admit: (id: number, residentId: number) => apiClient.post<Inquiry>(`/api/inquiries/${id}/admit`, { residentId }).then((r) => r.data),

  tours: (from: string, to: string) => apiClient.get<TourBooking[]>('/api/tour-bookings', { params: { from, to } }).then((r) => r.data),
  tourAction: (id: number, action: TourAction) => apiClient.post<TourBooking>(`/api/tour-bookings/${id}/${action}`).then((r) => r.data),
};
