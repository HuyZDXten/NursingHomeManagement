import { apiClient } from './client';
import type { AiHistoryItem, AiKnowledgeDoc, AiKnowledgeForm, AiPublicConfig } from '../types/operations';

/** S4-V1 — chatbot tư vấn công khai (ẩn danh) + quản lý tri thức (Admin). */
export const aiApi = {
  publicConfig: () => apiClient.get<AiPublicConfig>('/api/ai/public/config').then((r) => r.data),
  publicChat: (conversationKey: string, message: string) =>
    apiClient.post<{ reply: string }>('/api/ai/public/chat', { conversationKey, message }).then((r) => r.data.reply),
  publicHistory: (conversationKey: string) =>
    apiClient.get<AiHistoryItem[]>('/api/ai/public/history', { params: { conversationKey } }).then((r) => r.data),

  knowledge: () => apiClient.get<AiKnowledgeDoc[]>('/api/ai/knowledge').then((r) => r.data),
  createKnowledge: (body: AiKnowledgeForm) => apiClient.post<AiKnowledgeDoc>('/api/ai/knowledge', body).then((r) => r.data),
  updateKnowledge: (id: number, body: AiKnowledgeForm) => apiClient.put<AiKnowledgeDoc>(`/api/ai/knowledge/${id}`, body).then((r) => r.data),
  deleteKnowledge: (id: number) => apiClient.delete(`/api/ai/knowledge/${id}`),
};
