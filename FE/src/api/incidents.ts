import { apiClient } from './client';
import type { FamilyIncident, Incident, IncidentSeverity, IncidentStatus, IncidentType } from '../types/care';

export interface IncidentFilter {
  residentId?: number;
  status?: IncidentStatus;
  severity?: IncidentSeverity;
  from?: string;
  to?: string;
}

export interface ReportIncidentForm {
  residentId: number;
  type: IncidentType;
  severity: IncidentSeverity;
  occurredAt: string;
  location?: string;
  description: string;
  immediateAction?: string;
}

export interface UpdateIncidentForm {
  status: 'Investigating' | 'Closed';
  resolution?: string;
  shareWithFamily: boolean;
  notifyFamily: boolean;
  rowVersion: string;
}

export const incidentsApi = {
  list: (params?: IncidentFilter) =>
    apiClient.get<Incident[]>('/api/incidents', { params }).then((r) => r.data),
  get: (id: number) =>
    apiClient.get<Incident>(`/api/incidents/${id}`).then((r) => r.data),
  report: (body: ReportIncidentForm) =>
    apiClient.post<Incident>('/api/incidents', body).then((r) => r.data),
  update: (id: number, body: UpdateIncidentForm) =>
    apiClient.post<Incident>(`/api/incidents/${id}/updates`, body).then((r) => r.data),
  familyIncidents: (residentId: number) =>
    apiClient.get<FamilyIncident[]>(`/api/family/residents/${residentId}/incidents`).then((r) => r.data),
};
