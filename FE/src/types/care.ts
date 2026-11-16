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
