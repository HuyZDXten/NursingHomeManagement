import { apiClient } from './client';
import { odataList, type ODataQuery } from './odata';
import type { CareRecord, CareTask, MedicationDose, MedicationSchedule, VitalSign } from '../types';

export interface CareTaskForm {
  residentId: number;
  assignedToId: number;
  title: string;
  instructions?: string;
  dueDate: string;
  dueTime: string;
}

export interface VitalForm {
  residentId: number;
  systolic: number;
  diastolic: number;
  heartRate: number;
  temperature: number;
  spO2?: number | null;
  notes?: string;
  shareWithFamily: boolean;
}

export interface MedicationScheduleForm {
  residentId: number;
  medicationName: string;
  dose: string;
  route: string;
  startDate: string;
  endDate?: string | null;
  scheduledTime: string;
  approvalReference: string;
  instructions?: string;
}

export const careApi = {
  /** params: date (yyyy-MM-dd), mine, residentId. */
  tasks: (query: ODataQuery) => odataList<CareTask>('CareTasks', query),
  task: (id: number) => apiClient.get<CareTask>(`/api/care-tasks/${id}`).then((r) => r.data),
  createTask: (body: CareTaskForm) => apiClient.post<CareTask>('/api/care-tasks', body).then((r) => r.data),
  updateTask: (id: number, body: { status: string; result: string; shareWithFamily: boolean; rowVersion: string }) =>
    apiClient.post<CareTask>(`/api/care-tasks/${id}/updates`, body).then((r) => r.data),
  records: (id: number) => apiClient.get<CareRecord[]>(`/api/care-tasks/${id}/records`).then((r) => r.data),

  recordVital: (body: VitalForm) => apiClient.post<VitalSign>('/api/vitals', body).then((r) => r.data),

  schedules: (residentId?: number) =>
    apiClient.get<MedicationSchedule[]>('/api/medication-schedules', { params: { residentId } }).then((r) => r.data),
  createSchedule: (body: MedicationScheduleForm) => apiClient.post<MedicationSchedule>('/api/medication-schedules', body).then((r) => r.data),
  stopSchedule: (id: number) => apiClient.post(`/api/medication-schedules/${id}/stop`),
  today: (date?: string) => apiClient.get<MedicationDose[]>('/api/medications/today', { params: { date } }).then((r) => r.data),
  recordDose: (body: { scheduleId: number; date: string; status: string; notes?: string }) => apiClient.post('/api/medication-logs', body),
};
