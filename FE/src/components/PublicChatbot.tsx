import { useCallback, useEffect, useState } from 'react';
import { aiApi } from '../api/ai';
import type { AiPublicConfig } from '../types/operations';
import { ChatWidget, type ChatWidgetMessage } from './ChatWidget';

const KEY_STORAGE = 'nh.ai.publicConversationKey';

/** UUID của khách lưu localStorage để giữ hội thoại; trình duyệt chặn storage thì dùng UUID tạm cho phiên này. */
function conversationKey(): string {
  try {
    const saved = window.localStorage.getItem(KEY_STORAGE);
    if (saved) return saved;
    const created = crypto.randomUUID();
    window.localStorage.setItem(KEY_STORAGE, created);
    return created;
  } catch {
    return crypto.randomUUID();
  }
}

/** S4-V1 — UC-20: chatbot tư vấn trên mọi trang công khai. Ẩn khi Ai:Enabled hoặc Ai:PublicChatbotEnabled tắt. */
export function PublicChatbot() {
  const [config, setConfig] = useState<AiPublicConfig>();
  const [key] = useState(conversationKey);

  useEffect(() => {
    aiApi.publicConfig().then(setConfig).catch(() => undefined);
  }, []);

  const send = useCallback((text: string) => aiApi.publicChat(key, text), [key]);
  const loadHistory = useCallback(async (): Promise<ChatWidgetMessage[]> => aiApi.publicHistory(key), [key]);

  if (!config?.enabled) return null;
  return (
    <ChatWidget
      title="Trợ lý tư vấn"
      greeting={config.greeting}
      send={send}
      loadHistory={loadHistory}
      disclaimer="Trợ lý AI chỉ hỗ trợ thông tin chung, không thay thế tư vấn y tế."
    />
  );
}
