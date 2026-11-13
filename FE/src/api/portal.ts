import { apiClient } from './client';
import type { Dashboard, FamilyResident, FamilyResidentDetail, NotificationSummary, PublicOverview } from '../types';

export const portalApi = {
  publicOverview: () => apiClient.get<PublicOverview>('/api/public/overview').then((r) => r.data),
  dashboard: () => apiClient.get<Dashboard>('/api/dashboard').then((r) => r.data),

  familyResidents: () => apiClient.get<FamilyResident[]>('/api/family/residents').then((r) => r.data),
  familyResident: (id: number) => apiClient.get<FamilyResidentDetail>(`/api/family/residents/${id}`).then((r) => r.data),

  notifications: () => apiClient.get<NotificationSummary>('/api/notifications').then((r) => r.data),
  markRead: (id: number) => apiClient.post(`/api/notifications/${id}/read`),
  markAllRead: () => apiClient.post('/api/notifications/read-all'),
};
