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
  conditionStatus: {
    Active: { label: 'Đang điều trị', color: 'processing' },
    Resolved: { label: 'Đã ổn / khỏi', color: 'default' },
  },
  allergyType: {
    Drug: { label: 'Thuốc', color: 'red' },
    Food: { label: 'Thực phẩm', color: 'orange' },
    Other: { label: 'Khác', color: 'default' },
  },
  allergySeverity: {
    Mild: { label: 'Nhẹ', color: 'default' },
    Moderate: { label: 'Trung bình', color: 'warning' },
    Severe: { label: 'Nặng', color: 'error' },
  },
  dietType: {
    Normal: { label: 'Bình thường', color: 'default' },
    Diabetic: { label: 'Tiểu đường', color: 'blue' },
    LowSalt: { label: 'Ít muối', color: 'cyan' },
    LowFat: { label: 'Ít béo', color: 'geekblue' },
    Renal: { label: 'Bệnh thận', color: 'purple' },
    Vegetarian: { label: 'Chay', color: 'green' },
    Other: { label: 'Khác', color: 'default' },
  },
  texture: {
    Normal: { label: 'Cơm thường', color: 'default' },
    Soft: { label: 'Mềm / cháo', color: 'blue' },
    Minced: { label: 'Băm nhỏ', color: 'cyan' },
    Pureed: { label: 'Xay nhuyễn', color: 'orange' },
    Liquid: { label: 'Lỏng / súp', color: 'purple' },
  },
  visit: {
    Requested: { label: 'Chờ duyệt', color: 'warning' },
    Approved: { label: 'Đã duyệt', color: 'processing' },
    Rejected: { label: 'Từ chối', color: 'error' },
    CheckedIn: { label: 'Đã đến', color: 'cyan' },
    Completed: { label: 'Hoàn tất', color: 'success' },
    Cancelled: { label: 'Đã hủy', color: 'default' },
    NoShow: { label: 'Không đến', color: 'default' },
  },
  adlCategory: {
    Bathing: { label: 'Tắm rửa', color: 'cyan' },
    Toileting: { label: 'Vệ sinh', color: 'blue' },
    Mobility: { label: 'Vận động', color: 'geekblue' },
    Sleep: { label: 'Giấc ngủ', color: 'purple' },
    Mood: { label: 'Tâm trạng', color: 'gold' },
    Other: { label: 'Khác', color: 'default' },
  },
  adlLevel: {
    Independent: { label: 'Tự làm', color: 'success' },
    NeedsHelp: { label: 'Cần hỗ trợ', color: 'warning' },
    Dependent: { label: 'Phụ thuộc hoàn toàn', color: 'error' },
    Good: { label: 'Tốt', color: 'success' },
    Fair: { label: 'Trung bình', color: 'processing' },
    Poor: { label: 'Kém', color: 'error' },
  },
  mealType: {
    Breakfast: { label: 'Sáng', color: 'gold' },
    Lunch: { label: 'Trưa', color: 'orange' },
    Dinner: { label: 'Tối', color: 'purple' },
    Snack: { label: 'Phụ', color: 'cyan' },
  },
  assessmentType: {
    FallRisk: { label: 'Nguy cơ té ngã (Morse)', color: 'orange' },
    Barthel: { label: 'Độc lập sinh hoạt (Barthel)', color: 'blue' },
    Nutrition: { label: 'Dinh dưỡng (MNA-SF)', color: 'green' },
  },
  riskLevel: {
    Low: { label: 'Thấp / ổn định', color: 'success' },
    Medium: { label: 'Trung bình', color: 'warning' },
    High: { label: 'Cao / cần lưu ý', color: 'error' },
  },
  feedbackStatus: {
    New: { label: 'Mới nhận', color: 'error' },
    InProgress: { label: 'Đang xử lý', color: 'processing' },
    Resolved: { label: 'Đã trả lời', color: 'success' },
  },
  feedbackCategory: {
    Care: { label: 'Chăm sóc', color: 'blue' },
    Food: { label: 'Ăn uống', color: 'orange' },
    Facility: { label: 'Cơ sở vật chất', color: 'purple' },
    Billing: { label: 'Hóa đơn', color: 'gold' },
    Staff: { label: 'Nhân viên', color: 'cyan' },
    Other: { label: 'Khác', color: 'default' },
  },
  insightStatus: {
    Draft: { label: 'Chờ duyệt', color: 'warning' },
    Approved: { label: 'Đã duyệt', color: 'success' },
    Rejected: { label: 'Đã bỏ', color: 'default' },
  },
};
