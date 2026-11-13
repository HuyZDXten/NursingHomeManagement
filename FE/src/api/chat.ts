import { apiClient } from './client';
import type { ChatMessage, Conversation } from '../types/operations';

/** S3-V6 — UC-106 chat gia đình – nhân viên. Tin mới đến realtime qua sự kiện "chat" (hooks/useChat.ts). */
export const chatApi = {
  conversations: () => apiClient.get<Conversation[]>('/api/conversations').then((r) => r.data),
  open: (residentId: number) => apiClient.post<Conversation>('/api/conversations', { residentId }).then((r) => r.data),
  messages: (id: number, beforeId?: number, take = 30) =>
    apiClient.get<ChatMessage[]>(`/api/conversations/${id}/messages`, { params: { beforeId, take } }).then((r) => r.data),
  send: (id: number, body: string) => apiClient.post<ChatMessage>(`/api/conversations/${id}/messages`, { body }).then((r) => r.data),
  markRead: (id: number) => apiClient.post(`/api/conversations/${id}/read`),
};
