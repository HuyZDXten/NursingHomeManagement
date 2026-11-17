// BL-V5 (Viên): nhật ký thao tác, cấu hình & danh mục, thông báo hàng loạt, tin tức & album. Khớp NursingHome.DTOs.Admin.

export interface PageResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface AuditLog {
  id: number;
  accountId?: number | null;
  actorName: string;
  action: string;
  entityType: string;
  entityId?: string | null;
  summary: string;
  dataJson?: string | null;
  ip?: string | null;
  at: string;
}

export interface AuditQuery {
  from?: string;
  to?: string;
  action?: string;
  entityType?: string;
  search?: string;
  accountId?: number;
  page?: number;
  pageSize?: number;
}

export interface Setting {
  key: string;
  label: string;
  group: string;
  kind: 'Text' | 'Integer' | 'Decimal';
  value: string;
  defaultValue: string;
  isOverridden: boolean;
  min?: number | null;
  max?: number | null;
  updatedAt?: string | null;
}

export type LookupCategory = 'Relationship' | 'Department' | 'Position';

export const LOOKUP_CATEGORY_LABEL: Record<LookupCategory, string> = {
  Relationship: 'Quan hệ với cư dân',
  Department: 'Bộ phận',
  Position: 'Chức danh',
};

export interface Lookup {
  id: number;
  category: LookupCategory;
  code: string;
  name: string;
  sortOrder: number;
  isActive: boolean;
}

export interface LookupForm {
  category?: LookupCategory;
  code: string;
  name: string;
  sortOrder: number;
  isActive: boolean;
}

export type AnnouncementAudience = 'All' | 'Family' | 'Staff' | 'Role';

export interface Announcement {
  id: number;
  title: string;
  body: string;
  audience: AnnouncementAudience;
  role?: string | null;
  sendEmail: boolean;
  recipientCount: number;
  emailSentCount: number;
  createdByName: string;
  createdAt: string;
}

export interface AnnouncementForm {
  title: string;
  body: string;
  audience: AnnouncementAudience;
  role?: string;
  sendEmail: boolean;
}

export interface NotificationPreference {
  emailInvoices: boolean;
  emailReminders: boolean;
  emailAnnouncements: boolean;
}

export type PostKind = 'News' | 'Album';

export interface PostImage {
  id: number;
  url: string;
  caption?: string | null;
  sortOrder: number;
}

export interface PostSummary {
  id: number;
  kind: PostKind;
  title: string;
  slug: string;
  summary?: string | null;
  coverUrl?: string | null;
  isPublished: boolean;
  publishedAt?: string | null;
  imageCount: number;
  createdAt: string;
}

export interface Post extends PostSummary {
  content: string;
  consentConfirmed: boolean;
  createdByName: string;
  updatedAt?: string | null;
  images: PostImage[];
}

export interface PublicPost {
  id: number;
  kind: PostKind;
  title: string;
  slug: string;
  summary?: string | null;
  coverUrl?: string | null;
  publishedAt?: string | null;
  content: string;
  images: PostImage[];
}

export interface PostForm {
  kind: PostKind;
  title: string;
  summary?: string;
  content: string;
  coverUrl?: string;
  consentConfirmed: boolean;
  images: { url: string; caption?: string }[];
}
