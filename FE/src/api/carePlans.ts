import { apiClient } from './client';
import type {
  CarePlan,
  DailyCareCategory,
  DailyCareLevel,
  DailyCareLog,
  MealLog,
  MealType,
} from '../types/care';

export interface SaveCarePlanForm {
  residentId: number;
  title: string;
  goals?: string;
  startDate: string;
  endDate?: string | null;
  items: {
    id: number;
    title: string;
    instructions?: string;
    timeOfDay: string;
    daysOfWeek: string;
    defaultAssigneeId?: number | null;
  }[];
}

export interface CreateDailyLogForm {
  logDate: string;
  category: DailyCareCategory;
  level: DailyCareLevel;
  note?: string;
  shareWithFamily: boolean;
}

export interface UpsertMealForm {
  mealDate: string;
  meal: MealType;
  intakePercent: number;
  fluidMl?: number | null;
  note?: string;
  shareWithFamily: boolean;
}

export const carePlansApi = {
  listByResident: (residentId: number) =>
    apiClient.get<CarePlan[]>(`/api/residents/${residentId}/care-plans`).then((r) => r.data),
  create: (body: SaveCarePlanForm) =>
    apiClient.post<CarePlan>('/api/care-plans', body).then((r) => r.data),
  update: (id: number, body: SaveCarePlanForm) =>
    apiClient.put<CarePlan>(`/api/care-plans/${id}`, body).then((r) => r.data),
  deactivate: (id: number) =>
    apiClient.post(`/api/care-plans/${id}/deactivate`),
  generate: (days = 2, residentId?: number) =>
    apiClient.post<{ createdCount: number; skippedUnassignedCount: number }>('/api/care-plans/generate', null, {
      params: { days, residentId },
    }).then((r) => r.data),
};

export const dailyLogsApi = {
  listLogs: (residentId: number, date?: string) =>
    apiClient.get<DailyCareLog[]>(`/api/residents/${residentId}/daily-logs`, { params: { date } }).then((r) => r.data),
  addLog: (residentId: number, body: CreateDailyLogForm) =>
    apiClient.post<DailyCareLog>(`/api/residents/${residentId}/daily-logs`, body).then((r) => r.data),
  listMeals: (residentId: number, date?: string) =>
    apiClient.get<MealLog[]>(`/api/residents/${residentId}/meals`, { params: { date } }).then((r) => r.data),
  upsertMeal: (residentId: number, body: UpsertMealForm) =>
    apiClient.put<MealLog>(`/api/residents/${residentId}/meals`, body).then((r) => r.data),
};
