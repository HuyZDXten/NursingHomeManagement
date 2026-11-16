// Kiểu dữ liệu (khớp DTO) của module Chăm sóc & Gia đình — Huy.

export type IncidentType = 'Fall' | 'Injury' | 'Behavior' | 'Other';
export type IncidentSeverity = 'Low' | 'Medium' | 'High';
export type IncidentStatus = 'Open' | 'Investigating' | 'Closed';

export interface Incident {
  id: number;
  residentId: number;
  residentName: string;
  residentCode: string;
  type: IncidentType;
  severity: IncidentSeverity;
  occurredAt: string;
  location?: string | null;
  description: string;
  immediateAction?: string | null;
  reportedByName: string;
  status: IncidentStatus;
  resolution?: string | null;
  closedByName?: string | null;
  closedAt?: string | null;
  shareWithFamily: boolean;
  familyNotifiedAt?: string | null;
  createdAt: string;
  rowVersion: string;
}

export interface FamilyIncident {
  id: number;
  type: IncidentType;
  severity: IncidentSeverity;
  occurredAt: string;
  location?: string | null;
  description: string;
  immediateAction?: string | null;
  status: IncidentStatus;
  resolution?: string | null;
  closedAt?: string | null;
}

// S3-H2: bệnh nền, dị ứng, chế độ ăn & cảnh báo thuốc
export type ConditionStatus = 'Active' | 'Resolved';
export type AllergyType = 'Drug' | 'Food' | 'Other';
export type AllergySeverity = 'Mild' | 'Moderate' | 'Severe';
export type DietType = 'Normal' | 'Diabetic' | 'LowSalt' | 'LowFat' | 'Renal' | 'Vegetarian' | 'Other';
export type DietTexture = 'Normal' | 'Soft' | 'Minced' | 'Pureed' | 'Liquid';

export interface MedicalCondition {
  id: number;
  residentId: number;
  name: string;
  diagnosedOn?: string | null;
  status: ConditionStatus;
  notes?: string | null;
  recordedByName: string;
  createdAt: string;
}

export interface Allergy {
  id: number;
  residentId: number;
  allergen: string;
  type: AllergyType;
  reaction?: string | null;
  severity: AllergySeverity;
  recordedByName: string;
  createdAt: string;
}

export interface DietPlan {
  id: number;
  residentId: number;
  dietType: DietType;
  texture: DietTexture;
  restrictions?: string | null;
  startDate: string;
  endDate?: string | null;
  prescribedByName: string;
  createdAt: string;
}

export interface MedicalProfile {
  conditions: MedicalCondition[];
  allergies: Allergy[];
  activeDiet?: DietPlan | null;
  dietHistory: DietPlan[];
}

export interface MedicationWarning {
  kind: 'Allergy' | 'Duplicate';
  message: string;
}

// S3-H3: đặt & duyệt lịch thăm
export type VisitStatus = 'Requested' | 'Approved' | 'Rejected' | 'CheckedIn' | 'Completed' | 'Cancelled' | 'NoShow';

export interface VisitSlot {
  start: string;
  end: string;
  capacity: number;
  remaining: number;
  isPast: boolean;
}

export interface VisitBooking {
  id: number;
  residentId: number;
  residentName: string;
  residentCode: string;
  familyAccountId: number;
  familyName: string;
  familyPhone?: string | null;
  visitDate: string;
  slotStart: string;
  slotEnd: string;
  guestCount: number;
  guestNames?: string | null;
  note?: string | null;
  status: VisitStatus;
  reviewedByName?: string | null;
  reviewNote?: string | null;
  checkedInAt?: string | null;
  createdAt: string;
}

// S3-H4: kế hoạch chăm sóc, ADL, bữa ăn
export type DailyCareCategory = 'Bathing' | 'Toileting' | 'Mobility' | 'Sleep' | 'Mood' | 'Other';
export type DailyCareLevel = 'Independent' | 'NeedsHelp' | 'Dependent' | 'Good' | 'Fair' | 'Poor';
export type MealType = 'Breakfast' | 'Lunch' | 'Dinner' | 'Snack';

export interface CarePlanItem {
  id: number;
  title: string;
  instructions?: string | null;
  timeOfDay: string;
  daysOfWeek: string;
  defaultAssigneeId?: number | null;
  defaultAssigneeName?: string | null;
}

export interface CarePlan {
  id: number;
  residentId: number;
  title: string;
  goals?: string | null;
  startDate: string;
  endDate?: string | null;
  isActive: boolean;
  createdByName: string;
  createdAt: string;
  items: CarePlanItem[];
}

export interface DailyCareLog {
  id: number;
  residentId: number;
  logDate: string;
  category: DailyCareCategory;
  level: DailyCareLevel;
  note?: string | null;
  recordedByName: string;
  recordedAt: string;
  shareWithFamily: boolean;
}

export interface MealLog {
  id: number;
  residentId: number;
  mealDate: string;
  meal: MealType;
  intakePercent: number;
  fluidMl?: number | null;
  note?: string | null;
  recordedByName: string;
  recordedAt: string;
  shareWithFamily: boolean;
}

// S3-H5: đánh giá định kỳ, phiếu khám bác sĩ, ngưỡng sinh hiệu riêng
export type AssessmentType = 'FallRisk' | 'Barthel' | 'Nutrition';
export type RiskLevel = 'Low' | 'Medium' | 'High';

export interface AssessmentOption {
  key: string;
  label: string;
  points: number;
}

export interface AssessmentQuestion {
  key: string;
  label: string;
  options: AssessmentOption[];
}

export interface AssessmentScale {
  type: AssessmentType;
  title: string;
  maxScore: number;
  note: string;
  questions: AssessmentQuestion[];
}

export interface Assessment {
  id: number;
  residentId: number;
  type: AssessmentType;
  score: number;
  maxScore: number;
  riskLevel: RiskLevel;
  answers: Record<string, string>;
  note?: string | null;
  assessedByName: string;
  assessedAt: string;
  nextDueOn?: string | null;
}

export interface DueAssessment {
  residentId: number;
  residentCode: string;
  residentName: string;
  type: AssessmentType;
  lastAssessedAt?: string | null;
  lastRiskLevel?: RiskLevel | null;
  nextDueOn?: string | null;
}

export interface DoctorVisit {
  id: number;
  residentId: number;
  doctorId: number;
  doctorName: string;
  visitedAt: string;
  subjective?: string | null;
  objective?: string | null;
  assessment: string;
  plan?: string | null;
  followUpOn?: string | null;
  shareWithFamily: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface VitalThreshold {
  residentId: number;
  isCustom: boolean;
  systolicHigh: number;
  systolicLow: number;
  heartRateHigh: number;
  heartRateLow: number;
  temperatureHigh: number;
  spO2Low: number;
  note?: string | null;
  setByName?: string | null;
  updatedAt?: string | null;
}

// S3-H6: nhật ký hằng ngày & phản hồi gia đình
export type TimelineKind = 'Meal' | 'Adl' | 'Care' | 'Incident' | 'Vital' | 'DoctorVisit';
export type FeedbackCategory = 'Care' | 'Food' | 'Facility' | 'Billing' | 'Staff' | 'Other';
export type FeedbackStatus = 'New' | 'InProgress' | 'Resolved';

export interface TimelineItem {
  kind: TimelineKind;
  at: string;
  title: string;
  detail: string;
  recordedBy: string;
}

export interface Feedback {
  id: number;
  familyAccountId: number;
  familyName: string;
  residentId?: number | null;
  residentName?: string | null;
  category: FeedbackCategory;
  rating?: number | null;
  subject: string;
  content: string;
  status: FeedbackStatus;
  assignedToId?: number | null;
  assignedToName?: string | null;
  response?: string | null;
  respondedByName?: string | null;
  respondedAt?: string | null;
  createdAt: string;
}

// S4-H2: nhật ký hằng ngày AI & xu hướng sinh hiệu
export type InsightType = 'DailyDigest' | 'VitalTrend';
export type InsightStatus = 'Draft' | 'Approved' | 'Rejected';

export interface AiInsight {
  id: number;
  residentId: number;
  residentCode: string;
  residentName: string;
  type: InsightType;
  forDate: string;
  content: string;
  sourceJson?: string | null;
  status: InsightStatus;
  generatedAt: string;
  reviewedByName?: string | null;
  reviewedAt?: string | null;
  isAiGenerated: boolean;
  disclaimer: string;
}
