import type { Role } from '../types';
import { CARE_STATUS } from './status.care';
import { OPERATIONS_STATUS } from './status.operations';

export type StatusMeta = { label: string; color: string };
type Meta = StatusMeta;

/** Nhãn tiếng Việt + màu Tag (antd) của phần lõi. Module mới khai báo ở status.operations.ts / status.care.ts. */
const CORE_STATUS: Record<string, Record<string, Meta>> = {
  resident: {
    Active: { label: 'Đang lưu trú', color: 'green' },
    Discharged: { label: 'Đã ngừng lưu trú', color: 'default' },
  },
  bed: {
    Available: { label: 'Trống', color: 'green' },
    Occupied: { label: 'Có người', color: 'blue' },
    Maintenance: { label: 'Bảo trì', color: 'orange' },
  },
  care: {
    Pending: { label: 'Chờ làm', color: 'default' },
    InProgress: { label: 'Đang làm', color: 'processing' },
    Completed: { label: 'Hoàn thành', color: 'success' },
    Cancelled: { label: 'Đã hủy', color: 'error' },
  },
  medication: {
    Scheduled: { label: 'Chưa ghi nhận', color: 'default' },
    Taken: { label: 'Đã dùng', color: 'success' },
    Missed: { label: 'Bỏ lỡ', color: 'warning' },
    Refused: { label: 'Từ chối', color: 'error' },
  },
  invoice: {
    Draft: { label: 'Nháp', color: 'default' },
    Unpaid: { label: 'Chưa thanh toán', color: 'warning' },
    PartiallyPaid: { label: 'Trả một phần', color: 'processing' },
    Paid: { label: 'Đã thanh toán', color: 'success' },
    Cancelled: { label: 'Đã hủy', color: 'error' },
  },
  transaction: {
    Pending: { label: 'Chờ thanh toán', color: 'processing' },
    Paid: { label: 'Đã thanh toán', color: 'success' },
    Expired: { label: 'Hết hạn', color: 'default' },
    Cancelled: { label: 'Đã hủy', color: 'default' },
    Failed: { label: 'Lỗi', color: 'error' },
  },
  outcome: {
    APPLIED: { label: 'Đã ghi nhận', color: 'success' },
    DUPLICATE: { label: 'Trùng lặp', color: 'default' },
    UNMATCHED: { label: 'Không khớp', color: 'error' },
    REJECTED: { label: 'Từ chối', color: 'error' },
    SURPLUS: { label: 'Thừa tiền', color: 'warning' },
    IGNORED: { label: 'Bỏ qua', color: 'default' },
  },
  method: {
    Cash: { label: 'Tiền mặt', color: 'default' },
    BankTransfer: { label: 'Chuyển khoản', color: 'blue' },
    PayOS: { label: 'PayOS VietQR', color: 'purple' },
  },
};

/** Tất cả nhãn trạng thái: phần lõi + 2 module. */
export const STATUS: Record<string, Record<string, Meta>> = { ...CORE_STATUS, ...OPERATIONS_STATUS, ...CARE_STATUS };

export const ROLE_LABEL: Record<Role, string> = {
  Admin: 'Quản trị',
  Receptionist: 'Lễ tân',
  Accountant: 'Kế toán',
  Doctor: 'Bác sĩ',
  Nurse: 'Điều dưỡng',
  Caregiver: 'Hộ lý',
  Family: 'Gia đình',
};

export const statusMeta = (kind: string, value: string): Meta => STATUS[kind]?.[value] ?? { label: value, color: 'default' };

export const statusOptions = (kind: string) =>
  Object.entries(STATUS[kind] ?? {}).map(([value, meta]) => ({ value, label: meta.label }));
