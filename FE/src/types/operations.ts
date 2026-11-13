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

export interface CareAssignmentForm {
  residentId: number;
  accountId: number;
  shiftId?: number | null;
  startDate?: string;
}
