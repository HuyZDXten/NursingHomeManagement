import type { StatusMeta } from './status';

/**
 * Nhãn + màu trạng thái của module Chăm sóc & Gia đình (Huy). Gộp vào STATUS ở status.ts.
 */
export const CARE_STATUS: Record<string, Record<string, StatusMeta>> = {
  incident: {
    Open: { label: 'Mới báo', color: 'error' },
    Investigating: { label: 'Đang xử lý', color: 'processing' },
    Closed: { label: 'Đã đóng', color: 'default' },
  },
  incidentSeverity: {
    Low: { label: 'Thấp', color: 'default' },
    Medium: { label: 'Trung bình', color: 'warning' },
    High: { label: 'Cao', color: 'error' },
  },
  incidentType: {
    Fall: { label: 'Té ngã', color: 'volcano' },
    Injury: { label: 'Chấn thương', color: 'red' },
    Behavior: { label: 'Hành vi', color: 'purple' },
    Other: { label: 'Khác', color: 'default' },
  },
};
