import type { StatusMeta } from './status';

/**
 * Nhãn + màu trạng thái của module Chăm sóc & Gia đình (Huy). Gộp vào STATUS ở status.ts.
 * Ví dụ (S3-H1):
 *   incident: { Open: { label: 'Mới báo', color: 'error' }, Investigating: { label: 'Đang xử lý', color: 'processing' }, ... },
 */
export const CARE_STATUS: Record<string, Record<string, StatusMeta>> = {};
