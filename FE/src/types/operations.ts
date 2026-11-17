import type { InvoiceListItem } from './index';

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

// ---------------- S3-V5 dịch vụ phát sinh, hóa đơn hàng loạt, nhắc nợ ----------------

export interface ExtraService {
  id: number;
  name: string;
  unit: string;
  unitPrice: number;
  isActive: boolean;
}

export type ExtraServiceForm = Omit<ExtraService, 'id'>;

export interface ServiceUsage {
  id: number;
  residentId: number;
  residentCode: string;
  residentName: string;
  extraServiceId: number;
  serviceName: string;
  unit: string;
  usedOn: string;
  quantity: number;
  /** Đơn giá chốt lúc ghi. */
  unitPrice: number;
  amount: number;
  note?: string | null;
  recordedById: number;
  recordedByName: string;
  /** Đã vào hóa đơn → không xóa được. */
  invoiced: boolean;
  invoiceId?: number | null;
  createdAt: string;
}

export interface ServiceUsageForm {
  extraServiceId: number;
  usedOn?: string;
  quantity: number;
  note?: string;
}

export interface BatchSkip {
  residentId: number;
  residentCode: string;
  residentName: string;
  reason: string;
}

export interface BatchInvoiceResult {
  billingPeriod: string;
  created: InvoiceListItem[];
  skipped: BatchSkip[];
}

export interface ReminderRunResult {
  checked: number;
  sent: number;
}

export type ReminderStage = 'Before3Days' | 'Overdue' | 'Overdue7';

export interface InvoiceReminder {
  stage: ReminderStage;
  sentAt: string;
}

// ---------------- S3-V6 chat gia đình – nhân viên ----------------

export interface Conversation {
  id: number;
  residentId: number;
  residentCode: string;
  residentName: string;
  familyAccountId: number;
  familyName: string;
  lastMessage?: string | null;
  lastMessageAt?: string | null;
  /** Tin của bên kia mà mình chưa đọc. */
  unread: number;
}

export interface ChatMessage {
  id: number;
  conversationId: number;
  senderId: number;
  senderName: string;
  senderRole: string;
  fromFamily: boolean;
  body: string;
  sentAt: string;
  readAt?: string | null;
}

/** Sự kiện realtime "chat-read": ai đó vừa đọc các tin của bên kia. */
export interface ChatRead {
  conversationId: number;
  readerId: number;
  /** true = gia đình vừa đọc (tin của nhân viên thành "Đã xem"); false = nhân viên vừa đọc. */
  readerIsFamily: boolean;
  readAt: string;
}

/** Sự kiện realtime "chat-typing": bên kia đang soạn tin (chỉ báo tạm thời). */
export interface ChatTyping {
  conversationId: number;
  senderId: number;
  senderName: string;
  fromFamily: boolean;
}

// ---------------- S4-V1 AI: chatbot công khai + tri thức ----------------

export interface AiPublicConfig {
  enabled: boolean;
  greeting: string;
}

export interface AiHistoryItem {
  role: 'user' | 'assistant';
  content: string;
}

export interface AiKnowledgeDoc {
  id: number;
  title: string;
  content: string;
  tags?: string | null;
  /** false = chỉ trợ lý gia đình/nội bộ dùng. */
  isPublic: boolean;
  isActive: boolean;
  updatedAt: string;
}

export type AiKnowledgeForm = Omit<AiKnowledgeDoc, 'id' | 'updatedAt'>;

// ---------------- S4-V2 AI tóm tắt bàn giao + hỏi đáp báo cáo ----------------

export interface HandoverAiDraft {
  summary: string;
  /** false khi AI lỗi và đây là bản tóm tắt theo quy tắc. */
  aiGenerated: boolean;
  items: HandoverItem[];
  note?: string | null;
}

// ---------------- BL-V1 ảnh, đăng nhập Google, mã mời gia đình ----------------

export interface AuthConfig {
  /** Rỗng = máy chủ chưa cấu hình Google, ẩn nút. */
  googleClientId: string;
}

export type InvitationStatus = 'Pending' | 'Used' | 'Expired' | 'Revoked';

export interface FamilyInvitation {
  id: number;
  residentId: number;
  residentCode: string;
  residentName: string;
  email: string;
  relationship: string;
  canViewHealth: boolean;
  canViewCare: boolean;
  canViewMedication: boolean;
  canViewBilling: boolean;
  status: InvitationStatus;
  createdAt: string;
  expiresAt: string;
  usedAt?: string | null;
  createdByName?: string | null;
  accountName?: string | null;
}

export interface FamilyInvitationCreated {
  invitation: FamilyInvitation;
  /** Chỉ trả về một lần lúc tạo. */
  code: string;
  link: string;
  emailSent: boolean;
}

export interface FamilyInvitationForm {
  residentId: number;
  email: string;
  relationship: string;
  canViewHealth: boolean;
  canViewCare: boolean;
  canViewMedication: boolean;
  canViewBilling: boolean;
}

export interface InvitationLookup {
  email: string;
  residentName: string;
  relationship: string;
  expiresAt: string;
  facilityName: string;
}

// ---------------- BL-V2 hồ sơ lưu trú ----------------

export type DocumentKind = 'IdCard' | 'Insurance' | 'Medical' | 'Other';

export interface ResidentDocument {
  id: number;
  residentId: number;
  kind: DocumentKind;
  title: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedByName?: string | null;
  createdAt: string;
}

export interface ResidentContract {
  id: number;
  residentId: number;
  number: string;
  signedOn: string;
  startDate: string;
  endDate?: string | null;
  monthlyPrice?: number | null;
  notes?: string | null;
  hasFile: boolean;
  fileName?: string | null;
  status: 'Active' | 'Ended';
  createdByName?: string | null;
  createdAt: string;
  endedAt?: string | null;
}

export interface ContractForm {
  number: string;
  signedOn: string;
  startDate: string;
  endDate?: string | null;
  monthlyPrice?: number | null;
  notes?: string | null;
}

export type LeaveKind = 'Home' | 'Hospital' | 'Other';

export interface ResidentLeave {
  id: number;
  residentId: number;
  residentCode: string;
  residentName: string;
  kind: LeaveKind;
  startAt: string;
  expectedReturnOn?: string | null;
  returnedAt?: string | null;
  reason: string;
  createdByName?: string | null;
  isOpen: boolean;
  overdue: boolean;
}

export interface ResidentStay {
  id: number;
  admittedAt: string;
  dischargedAt: string;
}

export interface ReadmitForm {
  bedId: number;
  servicePackageId: number;
  agreedMonthlyPrice?: number | null;
  startDate: string;
}

export interface Deposit {
  id: number;
  residentId: number;
  amount: number;
  method: 'Cash' | 'BankTransfer';
  reference?: string | null;
  receivedAt: string;
  receivedByName?: string | null;
  refundedAt?: string | null;
  refundAmount?: number | null;
  refundNote?: string | null;
  refundedByName?: string | null;
  held: boolean;
}

export interface DepositSummary {
  residentStatus: string;
  heldAmount: number;
  outstanding: number;
  deposits: Deposit[];
}

// ---------------- BL-V3 phòng & giường mở rộng ----------------

export interface RoomType {
  id: number;
  name: string;
  dailySurcharge: number;
  description?: string | null;
  isActive: boolean;
}

export type RoomTypeForm = Omit<RoomType, 'id'>;

export interface BedReservation {
  id: number;
  bedId: number;
  bedLabel: string;
  inquiryId?: number | null;
  holderName: string;
  phone?: string | null;
  note?: string | null;
  status: 'Active' | 'Expired' | 'Converted' | 'Released';
  reservedByName?: string | null;
  reservedAt: string;
  expiresAt: string;
  closedAt?: string | null;
}

export interface BedReservationForm {
  bedId: number;
  inquiryId?: number | null;
  holderName: string;
  phone?: string | null;
  note?: string | null;
  expiresOn: string;
}

export type MaintenanceStatus = 'Open' | 'InProgress' | 'Done' | 'Cancelled';

export interface MaintenanceRequest {
  id: number;
  roomId: number;
  roomNumber: string;
  bedId?: number | null;
  bedNumber?: string | null;
  title: string;
  description?: string | null;
  priority: 'Low' | 'Normal' | 'High';
  status: MaintenanceStatus;
  blocksBed: boolean;
  reportedByName?: string | null;
  reportedAt: string;
  assignedTo?: string | null;
  resolvedAt?: string | null;
  resolutionNote?: string | null;
}

export interface MaintenanceForm {
  roomId: number;
  bedId?: number | null;
  title: string;
  description?: string | null;
  priority: 'Low' | 'Normal' | 'High';
  blocksBed: boolean;
}

// ---------------- UC-125 báo cáo sự cố ----------------

export interface IncidentMonth {
  year: number;
  month: number;
  label: string;
  total: number;
  closed: number;
  byType: Record<'Fall' | 'Injury' | 'Behavior' | 'Other', number>;
  bySeverity: Record<'Low' | 'Medium' | 'High', number>;
}

export interface IncidentReport {
  from: string;
  to: string;
  months: IncidentMonth[];
  totalByType: Record<string, number>;
  totalBySeverity: Record<string, number>;
  total: number;
  closed: number;
  closedRate?: number | null;
}

// ---------------- BL-V4 nhân sự ----------------

export type CertificateState = 'Valid' | 'ExpiringSoon' | 'Expired';

export interface StaffCertificate {
  id: number;
  accountId: number;
  accountName: string;
  name: string;
  number?: string | null;
  issuedBy?: string | null;
  issuedOn?: string | null;
  expiresOn?: string | null;
  state: CertificateState;
  daysLeft?: number | null;
}

export interface CertificateForm {
  name: string;
  number?: string | null;
  issuedBy?: string | null;
  issuedOn?: string | null;
  expiresOn?: string | null;
}

export interface StaffProfileSummary {
  accountId: number;
  fullName: string;
  email: string;
  role: string;
  isActive: boolean;
  position?: string | null;
  department?: string | null;
  hireDate?: string | null;
  certificateCount: number;
  expiringCount: number;
  expiredCount: number;
}

export interface StaffProfile {
  accountId: number;
  fullName: string;
  email: string;
  phone?: string | null;
  role: string;
  isActive: boolean;
  position?: string | null;
  department?: string | null;
  hireDate?: string | null;
  idNumber?: string | null;
  address?: string | null;
  emergencyContactName?: string | null;
  emergencyContactPhone?: string | null;
  notes?: string | null;
  updatedAt?: string | null;
  certificates: StaffCertificate[];
}

export type StaffProfileForm = Pick<StaffProfile, 'position' | 'department' | 'hireDate' | 'idNumber' | 'address' | 'emergencyContactName' | 'emergencyContactPhone' | 'notes'>;

export interface CertificateAlertResult {
  checked: number;
  sent: number;
}

export type ShiftStatus = 'Upcoming' | 'Open' | 'InProgress' | 'Done' | 'Missed';

export interface MyShift {
  shiftAssignmentId: number;
  workDate: string;
  shiftCode: string;
  shiftName: string;
  startsAt: string;
  endsAt: string;
  checkInAt?: string | null;
  checkOutAt?: string | null;
  lateMinutes: number;
  status: ShiftStatus;
  canCheckIn: boolean;
  canCheckOut: boolean;
}

export type AttendanceStatus = 'OnTime' | 'Late' | 'InProgress' | 'Absent' | 'Upcoming';

export interface AttendanceRecord {
  shiftAssignmentId: number;
  accountId: number;
  accountName: string;
  workDate: string;
  shiftName: string;
  startsAt: string;
  endsAt: string;
  checkInAt?: string | null;
  checkOutAt?: string | null;
  lateMinutes: number;
  status: AttendanceStatus;
  editReason?: string | null;
}

export interface StaffPerformanceRow {
  accountId: number;
  fullName: string;
  role: string;
  tasksDue: number;
  tasksOnTime: number;
  tasksLate: number;
  tasksOverdueOpen: number;
  onTimeRate?: number | null;
  medicationRecords: number;
  shiftsAssigned: number;
  shiftsAttended: number;
  lateCount: number;
  absent: number;
  attendanceRate?: number | null;
}

export interface StaffPerformanceReport {
  from: string;
  to: string;
  rows: StaffPerformanceRow[];
}

export interface HomeTask {
  id: number;
  residentId: number;
  residentCode: string;
  residentName: string;
  title: string;
  dueAt: string;
  status: 'Pending' | 'InProgress' | 'Completed';
  overdue: boolean;
}

export interface StaffHome {
  tasks: HomeTask[];
  tasksDone: number;
  tasksOpen: number;
  tasksOverdue: number;
  currentShift?: MyShift | null;
  nextShift?: MyShift | null;
  unreadHandovers: number;
  unreadMessages: number;
  certificates: StaffCertificate[];
}
