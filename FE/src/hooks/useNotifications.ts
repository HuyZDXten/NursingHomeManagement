import { useCallback, useEffect, useState } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { App } from 'antd';
import { API_BASE_URL, getFreshAccessToken } from '../api/client';
import { portalApi } from '../api/portal';
import type { AppNotification } from '../types';

/**
 * UC-117: danh sách thông báo + nhận realtime qua SignalR (/hubs/notifications).
 * DB là nguồn sự thật: mất kết nối thì lần tải sau vẫn đủ.
 */
export function useNotifications(enabled: boolean) {
  const { notification } = App.useApp();
  const [items, setItems] = useState<AppNotification[]>([]);
  const [unread, setUnread] = useState(0);

  const reload = useCallback(async () => {
    if (!enabled) return;
    try {
      const summary = await portalApi.notifications();
      setItems(summary.items);
      setUnread(summary.unreadCount);
    } catch {
      // Bỏ qua: chuông thông báo không được làm hỏng trang.
    }
  }, [enabled]);

  useEffect(() => {
    if (!enabled) return;
    void reload();
    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/notifications`, { accessTokenFactory: getFreshAccessToken })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connection.on('notification', (item: AppNotification) => {
      setItems((current) => [item, ...current].slice(0, 30));
      setUnread((count) => count + 1);
      notification.info({ message: item.title, description: item.body, placement: 'bottomRight' });
    });
    connection.start().catch(() => undefined);
    return () => {
      void connection.stop();
    };
  }, [enabled, reload, notification]);

  const markRead = async (id: number) => {
    await portalApi.markRead(id);
    setItems((current) => current.map((item) => (item.id === id ? { ...item, isRead: true } : item)));
    setUnread((count) => Math.max(0, count - 1));
  };

  const markAllRead = async () => {
    await portalApi.markAllRead();
    setItems((current) => current.map((item) => ({ ...item, isRead: true })));
    setUnread(0);
  };

  return { items, unread, reload, markRead, markAllRead };
}
