// Kiểu dữ liệu (khớp DTO) của module Vận hành & Tài chính — Viên.
// Thêm interface ở đây thay vì sửa types/index.ts (phần lõi).

// ---------------- S3-V1 tiếp nhận & tham quan ----------------

export type InquiryStatus = 'New' | 'Contacted' | 'Visited' | 'Admitted' | 'Lost';
export type InquirySource = 'Web' | 'Phone' | 'WalkIn' | 'Chatbot';
export type TourSlot = 'Morning' | 'Afternoon';
export type TourStatus = 'Requested' | 'Confirmed' | 'Done' | 'Cancelled' | 'NoShow';

/** Entity set OData "Inquiries". */
export interface Inquiry {
  id: number;
  fullName: string;
  phone: string;
  email?: string | null;
  elderName?: string | null;
  elderAge?: number | null;
  careNeeds?: string | null;
  interestedPackageId?: number | null;
  interestedPackageName?: string | null;
  source: InquirySource;
  status: InquiryStatus;
  assignedToId?: number | null;
  assignedToName?: string | null;
  notes?: string | null;
  residentId?: number | null;
  residentCode?: string | null;
  tourCount: number;
  lastTourDate?: string | null;
  lastTourSlot?: TourSlot | null;
  lastTourStatus?: TourStatus | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface TourBooking {
  id: number;
  inquiryId: number;
  fullName: string;
  phone: string;
  email?: string | null;
  visitDate: string;
  timeSlot: TourSlot;
  guestCount: number;
  note?: string | null;
  status: TourStatus;
  confirmedById?: number | null;
  confirmedByName?: string | null;
  createdAt: string;
}

/** GET /api/public/tour-slots — chỉ có số chỗ còn lại, không có thông tin khách. */
export interface TourSlotAvailability {
  date: string;
  slot: TourSlot;
  capacity: number;
  remaining: number;
}

export interface InquiryForm {
  fullName: string;
  phone: string;
  email?: string | null;
  elderName?: string | null;
  elderAge?: number | null;
  careNeeds?: string | null;
  interestedPackageId?: number | null;
}

export interface TourBookingForm {
  fullName: string;
  phone: string;
  email?: string | null;
  visitDate: string;
  timeSlot: TourSlot;
  guestCount: number;
  note?: string | null;
}

export interface UpdateInquiryForm {
  status: InquiryStatus;
  assignedToId?: number | null;
  notes?: string | null;
}

export interface PublicFormResult {
  message: string;
}

// ---------------- S3-V2 ca trực & phân công phụ trách ----------------

export type ShiftCode = 'MORNING' | 'AFTERNOON' | 'NIGHT';

export interface Shift {
  id: number;
  code: ShiftCode;
  name: string;
  /** "06:00:00" — giờ cơ sở. */
  startTime: string;
  endTime: string;
  crossesMidnight: boolean;
}

export interface ShiftAssignment {
  id: number;
  accountId: number;
  accountName: string;
  role: string;
  shiftId: number;
  shiftCode: ShiftCode;
  shiftName: string;
  workDate: string;
  /** UTC — ca đêm kết thúc ngày hôm sau. */
  startsAt: string;
  endsAt: string;
  note?: string | null;
  /** Chỉ có khi vừa tạo: làm hai ca liền nhau. */
  warning?: string | null;
}

export interface CopyWeekResult {
  copied: number;
  skipped: number;
}

export interface CareAssignment {
  id: number;
  residentId: number;
  residentCode: string;
  residentName: string;
  accountId: number;
  accountName: string;
  role: string;
  /** null = mọi ca. */
  shiftId?: number | null;
  shiftName?: string | null;
  startDate: string;
  endDate?: string | null;
  isActive: boolean;
  assignedByName: string;
}

// ---------------- S3-V3 báo cáo ----------------

export interface RevenueMonth {
  month: number;
  collected: number;
  collectedOnline: number;
  collectedManual: number;
  paymentCount: number;
}

export interface DebtRow {
  invoiceId: number;
  number: string;
  residentCode: string;
  residentName: string;
  billingPeriod: string;
  dueDate: string;
  status: string;
  total: number;
  paid: number;
  remaining: number;
  daysOverdue: number;
}

export interface RevenueReport {
  year: number;
  months: RevenueMonth[];
  totalCollected: number;
  totalDebt: number;
  debts: DebtRow[];
}

export interface OccupancyMonth {
  month: number;
  occupiedBedDays: number;
  availableBedDays: number;
  /** null = tháng chưa tới. */
  percent?: number | null;
}

export interface OccupancyReport {
  year: number;
  months: OccupancyMonth[];
  averagePercent?: number | null;
}

export interface AdherenceRow {
  residentId: number;
  code: string;
  fullName: string;
  taken: number;
  missed: number;
  refused: number;
  total: number;
  takenRate: number;
}

export interface AdherenceReport {
  from: string;
  to: string;
  rows: AdherenceRow[];
  overallTakenRate?: number | null;
}

export interface CareAssignmentForm {
  residentId: number;
  accountId: number;
  shiftId?: number | null;
  startDate?: string;
}

// ---------------- S3-V4 nghỉ phép/đổi ca & bàn giao ca ----------------

export type LeaveType = 'Leave' | 'Swap';
export type LeaveStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';

/** Một ca trong đơn; null khi ca đã bị bỏ khỏi lịch sau khi tạo đơn. */
export interface LeaveShift {
  assignmentId: number;
  workDate: string;
  shiftName: string;
  shiftCode: ShiftCode;
}

export interface LeaveRequest {
  id: number;
  accountId: number;
  accountName: string;
  role: string;
  type: LeaveType;
  fromDate: string;
  toDate: string;
  myShift?: LeaveShift | null;
  swapWithAccountId?: number | null;
  swapWithName?: string | null;
  theirShift?: LeaveShift | null;
  reason: string;
  status: LeaveStatus;
  reviewedByName?: string | null;
  reviewedAt?: string | null;
  reviewNote?: string | null;
  createdAt: string;
}

export interface LeaveApproval {
  request: LeaveRequest;
  /** Ca đã bỏ khi duyệt nghỉ — Admin cần xếp người thay. */
  vacatedShifts: LeaveShift[];
}

export interface LeaveRequestForm {
  type: LeaveType;
  fromDate?: string;
  toDate?: string;
  shiftAssignmentId?: number;
  swapShiftAssignmentId?: number;
  reason: string;
}

export type HandoverKind = 'Task' | 'Medication' | 'Vital' | 'Incident' | 'Note';

export interface HandoverItem {
  kind: HandoverKind;
  residentId?: number | null;
  residentName?: string | null;
  text: string;
}

export interface HandoverDraft {
  shiftId: number;
  shiftName: string;
  workDate: string;
  startsAt: string;
  endsAt: string;
  items: HandoverItem[];
}

export interface Handover {
  id: number;
  shiftId: number;
  shiftCode: ShiftCode;
  shiftName: string;
  workDate: string;
  startsAt: string;
  endsAt: string;
  fromAccountId: number;
  fromAccountName: string;
  role: string;
  summary: string;
  highlights: HandoverItem[];
  aiGenerated: boolean;
  acknowledgedByName?: string | null;
  acknowledgedAt?: string | null;
  createdAt: string;
  canAcknowledge: boolean;
}

export interface HandoverForm {
  shiftId: number;
  workDate: string;
  summary: string;
  highlights: HandoverItem[];
  aiGenerated?: boolean;
}
