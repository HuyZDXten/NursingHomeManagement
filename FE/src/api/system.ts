import { apiClient } from './client';
import type {
  Announcement, AnnouncementForm, AuditLog, AuditQuery, Lookup, LookupCategory, LookupForm, NotificationPreference, PageResult,
  Post, PostForm, PostKind, PostSummary, PublicPost, Setting,
} from '../types/system';

/** BL-V5: nhật ký thao tác, cấu hình & danh mục, thông báo hàng loạt, kênh nhận email, tin tức & album. */
export const systemApi = {
  // UC-13
  auditLogs: (params: AuditQuery) => apiClient.get<PageResult<AuditLog>>('/api/audit-logs', { params }).then((r) => r.data),
  auditActions: () => apiClient.get<string[]>('/api/audit-logs/actions').then((r) => r.data),

  // UC-129
  settings: () => apiClient.get<Setting[]>('/api/settings').then((r) => r.data),
  saveSettings: (values: { key: string; value: string }[]) => apiClient.put<Setting[]>('/api/settings', { values }).then((r) => r.data),
  resetSetting: (key: string) => apiClient.delete<Setting[]>('/api/settings', { params: { key } }).then((r) => r.data),

  // UC-130
  lookups: (category?: LookupCategory, includeInactive = false) =>
    apiClient.get<Lookup[]>('/api/lookups', { params: { category, includeInactive } }).then((r) => r.data),
  createLookup: (body: LookupForm) => apiClient.post<Lookup>('/api/lookups', body).then((r) => r.data),
  updateLookup: (id: number, body: LookupForm) => apiClient.put<Lookup>(`/api/lookups/${id}`, body).then((r) => r.data),
  deleteLookup: (id: number) => apiClient.delete(`/api/lookups/${id}`),

  // UC-119, UC-120
  announcements: () => apiClient.get<Announcement[]>('/api/announcements').then((r) => r.data),
  sendAnnouncement: (body: AnnouncementForm) => apiClient.post<Announcement>('/api/announcements', body).then((r) => r.data),
  myPreferences: () => apiClient.get<NotificationPreference>('/api/notification-preferences/me').then((r) => r.data),
  savePreferences: (body: NotificationPreference) => apiClient.put<NotificationPreference>('/api/notification-preferences/me', body).then((r) => r.data),

  // UC-19, UC-115 (quản trị)
  posts: (params: { kind?: PostKind; page?: number; pageSize?: number }) =>
    apiClient.get<PageResult<PostSummary>>('/api/posts', { params }).then((r) => r.data),
  post: (id: number) => apiClient.get<Post>(`/api/posts/${id}`).then((r) => r.data),
  createPost: (body: PostForm) => apiClient.post<Post>('/api/posts', body).then((r) => r.data),
  updatePost: (id: number, body: PostForm) => apiClient.put<Post>(`/api/posts/${id}`, body).then((r) => r.data),
  publishPost: (id: number) => apiClient.post<Post>(`/api/posts/${id}/publish`).then((r) => r.data),
  unpublishPost: (id: number) => apiClient.post<Post>(`/api/posts/${id}/unpublish`).then((r) => r.data),
  deletePost: (id: number) => apiClient.delete(`/api/posts/${id}`),
  uploadImage: (file: File) => {
    const form = new FormData();
    form.append('file', file);
    return apiClient.post<{ url: string }>('/api/uploads/image', form).then((r) => r.data);
  },

  // trang công khai (không cần đăng nhập)
  publicPosts: (params: { kind?: PostKind; page?: number; pageSize?: number }) =>
    apiClient.get<PageResult<PostSummary>>('/api/public/posts', { params }).then((r) => r.data),
  publicPost: (slug: string) => apiClient.get<PublicPost>(`/api/public/posts/${encodeURIComponent(slug)}`).then((r) => r.data),
};
