import { apiClient } from './client';
import type { AiInsight, InsightStatus, InsightType } from '../types/care';

export const aiInsightsApi = {
  list: (date?: string, type?: InsightType, status?: InsightStatus) =>
    apiClient.get<AiInsight[]>('/api/ai-insights', { params: { date, type, status } }).then((r) => r.data),
  approve: (id: number, content: string) =>
    apiClient.post<AiInsight>(`/api/ai-insights/${id}/approve`, { content }).then((r) => r.data),
  reject: (id: number) =>
    apiClient.post<AiInsight>(`/api/ai-insights/${id}/reject`).then((r) => r.data),
  generate: (type: InsightType = 'DailyDigest', date?: string) =>
    apiClient.post<{ createdCount: number }>('/api/ai-insights/generate', null, { params: { type, date } }).then((r) => r.data),
  vitalTrends: (residentId: number) =>
    apiClient.get<AiInsight[]>(`/api/residents/${residentId}/vital-trends`).then((r) => r.data),
  familyDigests: (residentId: number, date?: string) =>
    apiClient.get<AiInsight[]>(`/api/family/residents/${residentId}/digests`, { params: { date } }).then((r) => r.data),
};
