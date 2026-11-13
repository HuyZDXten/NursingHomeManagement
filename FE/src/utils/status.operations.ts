import type { StatusMeta } from './status';

/** Nhãn + màu trạng thái của module Vận hành & Tài chính (Viên). Gộp vào STATUS ở status.ts. */
export const OPERATIONS_STATUS: Record<string, Record<string, StatusMeta>> = {
  // S3-V1 tiếp nhận & tham quan
  inquiry: {
    New: { label: 'Mới', color: 'blue' },
    Contacted: { label: 'Đã liên hệ', color: 'processing' },
    Visited: { label: 'Đã tham quan', color: 'purple' },
    Admitted: { label: 'Đã nhập viện', color: 'success' },
    Lost: { label: 'Không tiếp tục', color: 'default' },
  },
  inquirySource: {
    Web: { label: 'Website', color: 'cyan' },
    Phone: { label: 'Điện thoại', color: 'geekblue' },
    WalkIn: { label: 'Đến trực tiếp', color: 'gold' },
    Chatbot: { label: 'Chatbot', color: 'magenta' },
  },
  tour: {
    Requested: { label: 'Chờ xác nhận', color: 'warning' },
    Confirmed: { label: 'Đã xác nhận', color: 'processing' },
    Done: { label: 'Đã đến', color: 'success' },
    Cancelled: { label: 'Đã hủy', color: 'default' },
    NoShow: { label: 'Không đến', color: 'error' },
  },
  tourSlot: {
    Morning: { label: 'Sáng (08:00–11:00)', color: 'gold' },
    Afternoon: { label: 'Chiều (14:00–17:00)', color: 'orange' },
  },
};
