import { apiClient } from './client';
import type { Bed, BedAssignment, Room } from '../types';

export interface RoomForm {
  number: string;
  floor?: number | null;
  description?: string | null;
  isActive: boolean;
}

export const roomsApi = {
  list: () => apiClient.get<Room[]>('/api/rooms').then((r) => r.data),
  create: (body: RoomForm) => apiClient.post<Room>('/api/rooms', body).then((r) => r.data),
  update: (id: number, body: RoomForm) => apiClient.put<Room>(`/api/rooms/${id}`, body).then((r) => r.data),
  remove: (id: number) => apiClient.delete(`/api/rooms/${id}`),
  addBed: (roomId: number, body: { number: string; maintenance: boolean }) =>
    apiClient.post<Bed>(`/api/rooms/${roomId}/beds`, body).then((r) => r.data),
  updateBed: (roomId: number, bedId: number, body: { number: string; maintenance: boolean }) =>
    apiClient.put<Bed>(`/api/rooms/${roomId}/beds/${bedId}`, body).then((r) => r.data),
  removeBed: (roomId: number, bedId: number) => apiClient.delete(`/api/rooms/${roomId}/beds/${bedId}`),
  /** Phân giường; cư dân đang có giường thì chuyển. */
  assign: (residentId: number, bedId: number) => apiClient.post('/api/bed-assignments', { residentId, bedId }),
  history: (residentId?: number) => apiClient.get<BedAssignment[]>('/api/bed-assignments', { params: { residentId } }).then((r) => r.data),
};
