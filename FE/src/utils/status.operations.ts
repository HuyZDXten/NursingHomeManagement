import type { StatusMeta } from './status';

/**
 * Nhãn + màu trạng thái của module Vận hành & Tài chính (Viên). Gộp vào STATUS ở status.ts.
 * Ví dụ (S3-V1):
 *   inquiry: { New: { label: 'Mới', color: 'blue' }, Contacted: { label: 'Đã liên hệ', color: 'processing' }, ... },
 */
export const OPERATIONS_STATUS: Record<string, Record<string, StatusMeta>> = {};
