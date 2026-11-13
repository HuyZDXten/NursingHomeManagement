// Kiểu dữ liệu khớp DTO của API (NursingHome.DTOs). Ngày giờ (DateTime) là chuỗi ISO UTC có "Z";
// ngày (DateOnly) là "yyyy-MM-dd"; giờ (TimeOnly) là "HH:mm:ss".

export const ROLES = ['Admin', 'Receptionist', 'Accountant', 'Doctor', 'Nurse', 'Caregiver', 'Family'] as const;
export type Role = (typeof ROLES)[number];

export interface ApiError {
  message: string;
}

export interface LoginResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  accountId: number;
  fullName: string;
  email: string;
  role: Role;
}

export interface Profile {
  id: number;
  email: string;
  fullName: string;
  phone?: string | null;
  role: Role;
  avatarUrl?: string | null;
}

export interface Account {
  id: number;
  email: string;
  fullName: string;
  phone?: string | null;
  role: Role;
  isActive: boolean;
  createdAt: string;
}

export interface LookupItem {
  id: number;
  name: string;
  extra?: string | null;
}

export interface ODataPage<T> {
  items: T[];
  total: number;
}

// ---------------- Cư dân ----------------
export interface ResidentListItem {
  id: number;
  code: string;
  fullName: string;
  dateOfBirth: string;
  age: number;
  gender: string;
  status: 'Active' | 'Discharged';
  admittedAt: string;
  dischargedAt?: string | null;
  bedId?: number | null;
  bedLabel?: string | null;
}

export interface ResidentDetail extends ResidentListItem {
  notes?: string | null;
  photoUrl?: string | null;
}

export interface Contact {
  id: number;
  residentId: number;
  fullName: string;
  relationship: string;
  phone: string;
  email?: string | null;
  isEmergencyContact: boolean;
}

export interface FamilyLink {
  id: number;
  residentId: number;
  familyAccountId: number;
  familyName: string;
  familyEmail: string;
  canViewHealth: boolean;
  canViewCare: boolean;
  canViewMedication: boolean;
  canViewBilling: boolean;
}

// ---------------- Phòng ----------------
export interface Bed {
  id: number;
  roomId: number;
  number: string;
  status: 'Available' | 'Occupied' | 'Maintenance';
  residentId?: number | null;
  residentName?: string | null;
  residentCode?: string | null;
}

export interface Room {
  id: number;
  number: string;
  floor?: number | null;
  description?: string | null;
  isActive: boolean;
  beds: Bed[];
}

export interface BedAssignment {
  id: number;
  residentId: number;
  residentName: string;
  roomNumber: string;
  bedNumber: string;
  startedAt: string;
  endedAt?: string | null;
  assignedByName: string;
}

// ---------------- Chăm sóc & sức khỏe ----------------
export type CareTaskStatus = 'Pending' | 'InProgress' | 'Completed' | 'Cancelled';

export interface CareTask {
  id: number;
  residentId: number;
  residentName: string;
  residentCode: string;
  title: string;
  instructions?: string | null;
  assignedToId: number;
  assignedToName: string;
  dueAt: string;
  status: CareTaskStatus;
  rowVersion: string;
}

export interface CareRecord {
  id: number;
  careTaskId: number;
  taskTitle: string;
  status: CareTaskStatus;
  result: string;
  performedAt: string;
  recordedByName: string;
  shareWithFamily: boolean;
}

export interface VitalSign {
  id: number;
  residentId: number;
  systolic: number;
  diastolic: number;
  heartRate: number;
  temperature: number;
  spO2?: number | null;
  notes?: string | null;
  recordedAt: string;
  recordedByName: string;
  shareWithFamily: boolean;
}

// ---------------- Thuốc ----------------
export interface MedicationSchedule {
  id: number;
  residentId: number;
  residentName: string;
  medicationName: string;
  dose: string;
  route: string;
  startDate: string;
  endDate?: string | null;
  scheduledTime: string;
  approvalReference: string;
  instructions?: string | null;
  enteredByName: string;
  isActive: boolean;
}

export type MedicationStatus = 'Scheduled' | 'Taken' | 'Missed' | 'Refused';

export interface MedicationDose {
  scheduleId: number;
  residentId: number;
  residentName: string;
  residentCode: string;
  medicationName: string;
  dose: string;
  route: string;
  instructions?: string | null;
  scheduledTime: string;
  scheduledAt: string;
  status: MedicationStatus;
  recordedAt?: string | null;
  recordedByName?: string | null;
  notes?: string | null;
  canRecord: boolean;
}

export interface MedicationLog {
  id: number;
  scheduleId: number;
  medicationName: string;
  dose: string;
  scheduledAt: string;
  recordedAt?: string | null;
  status: MedicationStatus;
  recordedByName?: string | null;
  notes?: string | null;
}

// ---------------- Gói, hóa đơn, thanh toán ----------------
export interface ServicePackage {
  id: number;
  name: string;
  monthlyPrice: number;
  description?: string | null;
  isActive: boolean;
}

export interface Subscription {
  id: number;
  residentId: number;
  residentName: string;
  servicePackageId: number;
  packageName: string;
  startDate: string;
  endDate?: string | null;
  agreedMonthlyPrice: number;
}

export type InvoiceStatus = 'Draft' | 'Unpaid' | 'PartiallyPaid' | 'Paid' | 'Cancelled';

export interface InvoiceListItem {
  id: number;
  number: string;
  residentId: number;
  residentName: string;
  residentCode: string;
  billingPeriod: string;
  dueDate: string;
  issuedAt?: string | null;
  status: InvoiceStatus;
  total: number;
  paid: number;
  remaining: number;
}

export interface InvoiceItem {
  id: number;
  description: string;
  quantity: number;
  unitPrice: number;
  amount: number;
}

export interface Payment {
  id: number;
  amount: number;
  method: 'Cash' | 'BankTransfer' | 'PayOS';
  paidAt: string;
  reference: string;
}

export interface InvoiceDetail extends InvoiceListItem {
  items: InvoiceItem[];
  payments: Payment[];
}

export interface Checkout {
  transactionId: number;
  invoiceId: number;
  invoiceNumber: string;
  amount: number;
  status: string;
  checkoutUrl?: string | null;
  qrCode?: string | null;
  bankName?: string | null;
  accountNumber?: string | null;
  accountName?: string | null;
  description?: string | null;
  expiresAt?: string | null;
}

export interface PaymentStatus {
  transactionId: number;
  invoiceId: number;
  status: 'Pending' | 'Paid' | 'Expired' | 'Cancelled' | 'Failed';
  invoiceStatus: InvoiceStatus | '';
  amount: number;
  paidAt?: string | null;
}

export interface PaymentTransaction {
  id: number;
  invoiceId: number;
  invoiceNumber: string;
  residentName: string;
  amount: number;
  status: PaymentStatus['status'];
  orderCode?: number | null;
  attempt: number;
  createdAt: string;
  expiresAt?: string | null;
  paidAt?: string | null;
}

export interface WebhookLog {
  id: number;
  source: 'Webhook' | 'Sync';
  signatureValid: boolean;
  outcome: 'APPLIED' | 'DUPLICATE' | 'UNMATCHED' | 'REJECTED' | 'SURPLUS' | 'IGNORED';
  reason?: string | null;
  orderCode?: number | null;
  amount?: number | null;
  providerEventId?: string | null;
  paymentTransactionId?: number | null;
  receivedAt: string;
}

// ---------------- Cổng Family, dashboard, thông báo ----------------
export interface FamilyResident {
  residentId: number;
  code: string;
  fullName: string;
  dateOfBirth: string;
  status: string;
  canViewHealth: boolean;
  canViewCare: boolean;
  canViewMedication: boolean;
  canViewBilling: boolean;
}

export interface FamilyResidentDetail {
  resident: FamilyResident;
  bedLabel?: string | null;
  vitals: VitalSign[];
  careRecords: CareRecord[];
  medications: MedicationSchedule[];
  invoices: InvoiceListItem[];
}

export interface Dashboard {
  activeResidents: number;
  rooms: number;
  beds: number;
  availableBeds: number;
  occupiedBeds: number;
  maintenanceBeds: number;
  tasksToday: number;
  openTasks: number;
  billed: number;
  collected: number;
  debt: number;
  unpaidInvoices: number;
  pendingOnlinePayments: number;
  collectedByMonth: { month: string; amount: number }[];
}

export interface PublicOverview {
  facilityName: string;
  address?: string | null;
  hotline?: string | null;
  visitingHours?: string | null;
  availableBeds: number;
  totalBeds: number;
  packages: ServicePackage[];
}

export interface AppNotification {
  id: number;
  type: 'Payment' | 'Invoice' | 'Care' | 'Health' | 'System';
  title: string;
  body?: string | null;
  link?: string | null;
  isRead: boolean;
  createdAt: string;
}

export interface NotificationSummary {
  unreadCount: number;
  items: AppNotification[];
}
