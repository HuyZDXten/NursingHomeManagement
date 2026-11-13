import { useEffect, useRef } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { API_BASE_URL, getFreshAccessToken } from '../api/client';
import type { ChatMessage } from '../types/operations';

/**
 * S3-V6: nghe sự kiện "chat" trên hub /hubs/notifications (dùng lại hub của chuông thông báo).
 * Callback mới nhất luôn được gọi (ref), nên không phải kết nối lại khi state của trang đổi.
 */
export function useChat(onMessage: (message: ChatMessage) => void) {
  const handler = useRef(onMessage);
  useEffect(() => {
    handler.current = onMessage;
  }, [onMessage]);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/notifications`, { accessTokenFactory: getFreshAccessToken })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connection.on('chat', (message: ChatMessage) => handler.current(message));
    connection.start().catch(() => undefined);
    return () => {
      void connection.stop();
    };
  }, []);
}
