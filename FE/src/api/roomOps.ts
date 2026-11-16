import { apiClient } from './client';
import type {
  BedReservation, BedReservationForm, MaintenanceForm, MaintenanceRequest, MaintenanceStatus, RoomType, RoomTypeForm,
} from '../types/operations';

/** BL-V3: loại phòng & phụ thu, giữ giường, báo hỏng & sửa chữa. */
export const roomOpsApi = {
  roomTypes: (activeOnly = false) => apiClient.get<RoomType[]>('/api/room-types', { params: { activeOnly } }).then((r) => r.data),
  saveRoomType: (id: number | null, body: RoomTypeForm) =>
    (id ? apiClient.put<RoomType>(`/api/room-types/${id}`, body) : apiClient.post<RoomType>('/api/room-types', body)).then((r) => r.data),

  reservations: (activeOnly = false) =>
    apiClient.get<BedReservation[]>('/api/bed-reservations', { params: { activeOnly } }).then((r) => r.data),
  reserve: (body: BedReservationForm) => apiClient.post<BedReservation>('/api/bed-reservations', body).then((r) => r.data),
  release: (id: number) => apiClient.post<BedReservation>(`/api/bed-reservations/${id}/release`).then((r) => r.data),

  maintenance: (status?: MaintenanceStatus) =>
    apiClient.get<MaintenanceRequest[]>('/api/maintenance-requests', { params: { status } }).then((r) => r.data),
  report: (body: MaintenanceForm) => apiClient.post<MaintenanceRequest>('/api/maintenance-requests', body).then((r) => r.data),
  updateStatus: (id: number, body: { status: Exclude<MaintenanceStatus, 'Open'>; assignedTo?: string; resolutionNote?: string }) =>
    apiClient.post<MaintenanceRequest>(`/api/maintenance-requests/${id}/status`, body).then((r) => r.data),
};
