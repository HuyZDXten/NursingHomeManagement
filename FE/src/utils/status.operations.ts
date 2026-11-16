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
  // S3-V2 ca trực
  shift: {
    MORNING: { label: 'Ca sáng', color: 'gold' },
    AFTERNOON: { label: 'Ca chiều', color: 'blue' },
    NIGHT: { label: 'Ca đêm', color: 'purple' },
  },
  // S3-V4 nghỉ phép/đổi ca & bàn giao ca
  leave: {
    Pending: { label: 'Chờ duyệt', color: 'warning' },
    Approved: { label: 'Đã duyệt', color: 'success' },
    Rejected: { label: 'Từ chối', color: 'error' },
    Cancelled: { label: 'Đã hủy', color: 'default' },
  },
  leaveType: {
    Leave: { label: 'Nghỉ phép', color: 'volcano' },
    Swap: { label: 'Đổi ca', color: 'geekblue' },
  },
  handoverKind: {
    Task: { label: 'Việc chưa xong', color: 'orange' },
    Medication: { label: 'Thuốc', color: 'magenta' },
    Vital: { label: 'Sinh hiệu', color: 'red' },
    Incident: { label: 'Sự cố', color: 'volcano' },
    Note: { label: 'Ghi chú', color: 'default' },
  },
  // S3-V5 nhắc nợ
  reminderStage: {
    Before3Days: { label: 'Sắp đến hạn (≤ 3 ngày)', color: 'gold' },
    Overdue: { label: 'Quá hạn', color: 'orange' },
    Overdue7: { label: 'Quá hạn ≥ 7 ngày', color: 'red' },
  },
  // BL-V1 mã mời tài khoản gia đình
  invitation: {
    Pending: { label: 'Đang chờ', color: 'processing' },
    Used: { label: 'Đã kích hoạt', color: 'success' },
    Expired: { label: 'Hết hạn', color: 'default' },
    Revoked: { label: 'Đã thu hồi', color: 'error' },
  },
  // BL-V2 hồ sơ lưu trú
  documentKind: {
    IdCard: { label: 'CCCD/CMND', color: 'blue' },
    Insurance: { label: 'Thẻ BHYT', color: 'cyan' },
    Medical: { label: 'Giấy tờ y tế', color: 'purple' },
    Other: { label: 'Khác', color: 'default' },
  },
  contract: {
    Active: { label: 'Đang hiệu lực', color: 'success' },
    Ended: { label: 'Đã kết thúc', color: 'default' },
  },
  leaveKind: {
    Home: { label: 'Về nhà', color: 'blue' },
    Hospital: { label: 'Nằm viện', color: 'volcano' },
    Other: { label: 'Khác', color: 'default' },
  },
  depositMethod: {
    Cash: { label: 'Tiền mặt', color: 'gold' },
    BankTransfer: { label: 'Chuyển khoản', color: 'geekblue' },
  },
  // BL-V3 phòng & giường mở rộng
  reservation: {
    Active: { label: 'Đang giữ', color: 'purple' },
    Expired: { label: 'Hết hạn', color: 'default' },
    Converted: { label: 'Đã nhập viện', color: 'success' },
    Released: { label: 'Đã hủy', color: 'default' },
  },
  maintenance: {
    Open: { label: 'Mới báo', color: 'orange' },
    InProgress: { label: 'Đang sửa', color: 'processing' },
    Done: { label: 'Đã xong', color: 'success' },
    Cancelled: { label: 'Đã hủy', color: 'default' },
  },
  priority: {
    Low: { label: 'Thấp', color: 'default' },
    Normal: { label: 'Bình thường', color: 'blue' },
    High: { label: 'Khẩn', color: 'red' },
  },
};