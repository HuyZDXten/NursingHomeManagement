import { useCallback, useEffect, useRef } from 'react';
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';
import { API_BASE_URL, getFreshAccessToken } from '../api/client';
import type { ChatMessage, ChatRead, ChatTyping } from '../types/operations';

export interface ChatHandlers {
  /** Tin mới (cả tin của chính mình gửi từ tab khác). */
  onMessage: (message: ChatMessage) => void;
  /** Bên kia vừa đọc: các tin mình đã gửi được đánh dấu "Đã xem". */
  onRead?: (event: ChatRead) => void;
  /** Bên kia đang soạn tin. */
  onTyping?: (event: ChatTyping) => void;
  /** SignalR vừa tự nối lại sau khi mất kết nối: tải lại dữ liệu có thể đã lỡ. */
  onReconnected?: () => void;
}

/** Khoảng tối thiểu giữa hai lần báo "đang nhập" của cùng một cuộc trò chuyện (server cũng giới hạn). */
const TYPING_INTERVAL_MS = 2000;

/**
 * S3-V6: nghe các sự kiện chat trên hub /hubs/notifications (dùng lại hub của chuông thông báo):
 * "chat" (tin mới), "chat-read" (Đã xem), "chat-typing" (đang nhập). Gọi ngược lên server bằng invoke("Typing").
 * Callback mới nhất luôn được gọi (ref), nên không phải kết nối lại khi state của trang đổi.
 * Mất kết nối thì SignalR tự nối lại, rồi gọi onReconnected để trang tải lại phần đã lỡ (DB là nguồn sự thật).
 */
export function useChat(handlers: ChatHandlers) {
  const latest = useRef(handlers);
  useEffect(() => {
    latest.current = handlers;
  }, [handlers]);

  const connectionRef = useRef<HubConnection | null>(null);
  const lastTyping = useRef<Record<number, number>>({});

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/notifications`, { accessTokenFactory: getFreshAccessToken })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connection.on('chat', (message: ChatMessage) => latest.current.onMessage(message));
    connection.on('chat-read', (event: ChatRead) => latest.current.onRead?.(event));
    connection.on('chat-typing', (event: ChatTyping) => latest.current.onTyping?.(event));
    connection.onreconnected(() => latest.current.onReconnected?.());
    connectionRef.current = connection;
    connection.start().catch(() => undefined);
    return () => {
      connectionRef.current = null;
      void connection.stop();
    };
  }, []);

  /** Báo bên kia "đang nhập". Gọi thoải mái khi gõ phím: tự giãn cách, lỗi mạng bị bỏ qua. */
  const notifyTyping = useCallback((conversationId: number) => {
    const connection = connectionRef.current;
    if (connection?.state !== HubConnectionState.Connected) return;
    const now = Date.now();
    if (now - (lastTyping.current[conversationId] ?? 0) < TYPING_INTERVAL_MS) return;
    lastTyping.current[conversationId] = now;
    connection.invoke('Typing', conversationId).catch(() => undefined);
  }, []);

  return { notifyTyping };
}
