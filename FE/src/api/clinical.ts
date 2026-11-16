import { apiClient } from './client';
import type {
  Assessment,
  AssessmentScale,
  AssessmentType,
  DoctorVisit,
  DueAssessment,
  VitalThreshold,
} from '../types/care';

export interface SubmitAssessmentForm {
  residentId: number;
  type: AssessmentType;
  answers: Record<string, string>;
  note?: string;
}

export interface SaveDoctorVisitForm {
  visitedAt?: string;
  subjective?: string;
  objective?: string;
  assessment: string;
  plan?: string;
  followUpOn?: string | null;
  shareWithFamily: boolean;
}

export interface SaveThresholdForm {
  systolicHigh: number;
  systolicLow: number;
  heartRateHigh: number;
  heartRateLow: number;
  temperatureHigh: number;
  spO2Low: number;
  note?: string;
}

export const clinicalApi = {
  scales: () =>
    apiClient.get<AssessmentScale[]>('/api/assessments/scales').then((r) => r.data),
  listAssessments: (residentId: number) =>
    apiClient.get<Assessment[]>(`/api/residents/${residentId}/assessments`).then((r) => r.data),
  submitAssessment: (body: SubmitAssessmentForm) =>
    apiClient.post<Assessment>('/api/assessments', body).then((r) => r.data),
  dueAssessments: () =>
    apiClient.get<DueAssessment[]>('/api/assessments/due').then((r) => r.data),

  listDoctorVisits: (residentId: number) =>
    apiClient.get<DoctorVisit[]>(`/api/residents/${residentId}/doctor-visits`).then((r) => r.data),
  createDoctorVisit: (residentId: number, body: SaveDoctorVisitForm) =>
    apiClient.post<DoctorVisit>(`/api/residents/${residentId}/doctor-visits`, body).then((r) => r.data),
  updateDoctorVisit: (residentId: number, visitId: number, body: SaveDoctorVisitForm) =>
    apiClient.put<DoctorVisit>(`/api/residents/${residentId}/doctor-visits/${visitId}`, body).then((r) => r.data),

  getThreshold: (residentId: number) =>
    apiClient.get<VitalThreshold>(`/api/residents/${residentId}/vital-thresholds`).then((r) => r.data),
  setThreshold: (residentId: number, body: SaveThresholdForm) =>
    apiClient.put<VitalThreshold>(`/api/residents/${residentId}/vital-thresholds`, body).then((r) => r.data),
};
