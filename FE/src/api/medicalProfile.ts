import { apiClient } from './client';
import type {
  Allergy,
  AllergySeverity,
  AllergyType,
  ConditionStatus,
  DietPlan,
  DietTexture,
  DietType,
  MedicalCondition,
  MedicalProfile,
  MedicationWarning,
} from '../types/care';

export interface SaveConditionForm {
  name: string;
  diagnosedOn?: string | null;
  status: ConditionStatus;
  notes?: string;
}

export interface SaveAllergyForm {
  allergen: string;
  type: AllergyType;
  reaction?: string;
  severity: AllergySeverity;
}

export interface SaveDietPlanForm {
  dietType: DietType;
  texture: DietTexture;
  restrictions?: string;
  startDate?: string;
}

export const medicalProfileApi = {
  get: (residentId: number) =>
    apiClient.get<MedicalProfile>(`/api/residents/${residentId}/medical-profile`).then((r) => r.data),
  familyGet: (residentId: number) =>
    apiClient.get<MedicalProfile>(`/api/family/residents/${residentId}/medical-profile`).then((r) => r.data),
  addCondition: (residentId: number, body: SaveConditionForm) =>
    apiClient.post<MedicalCondition>(`/api/residents/${residentId}/conditions`, body).then((r) => r.data),
  updateCondition: (residentId: number, conditionId: number, body: SaveConditionForm) =>
    apiClient.put<MedicalCondition>(`/api/residents/${residentId}/conditions/${conditionId}`, body).then((r) => r.data),
  deleteCondition: (residentId: number, conditionId: number) =>
    apiClient.delete(`/api/residents/${residentId}/conditions/${conditionId}`),
  addAllergy: (residentId: number, body: SaveAllergyForm) =>
    apiClient.post<Allergy>(`/api/residents/${residentId}/allergies`, body).then((r) => r.data),
  deleteAllergy: (residentId: number, allergyId: number) =>
    apiClient.delete(`/api/residents/${residentId}/allergies/${allergyId}`),
  setDietPlan: (residentId: number, body: SaveDietPlanForm) =>
    apiClient.post<DietPlan>(`/api/residents/${residentId}/diet-plans`, body).then((r) => r.data),
  checkMedication: (residentId: number, medicationName: string) =>
    apiClient
      .post<MedicationWarning[]>('/api/medication-schedules/check', { residentId, medicationName })
      .then((r) => r.data),
};
