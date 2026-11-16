import { apiClient } from './client';
import type { ChatWidgetMessage } from '../components/ChatWidget';

export const familyAssistantApi = {
  chat: (message: string) =>
    apiClient.post<{ reply: string }>('/api/ai/family/chat', { message }).then((r) => r.data.reply),
  history: () =>
    apiClient.get<ChatWidgetMessage[]>('/api/ai/family/history').then((r) => r.data),
};
