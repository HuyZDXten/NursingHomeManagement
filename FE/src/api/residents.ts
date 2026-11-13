import { apiClient } from './client';
import { odataList, type ODataQuery } from './odata';
import type {
  BedAssignment, Contact, FamilyLink, MedicationLog, ResidentDetail, ResidentListItem, Subscription, VitalSign,
} from '../types';

export interface ResidentForm {
  code: string;
  fullName: string;
  dateOfBirth: string;
  gender: string;
  notes?: string | null;
  photoUrl?: string | null;
}

export type ContactForm = Omit<Contact, 'id' | 'residentId'>;
export type FamilyLinkForm = Pick<FamilyLink, 'familyAccountId' | 'canViewHealth' | 'canViewCare' | 'canViewMedication' | 'canViewBilling'>;

export const residentsApi = {
  list: (query: ODataQuery) => odataList<ResidentListItem>('Residents', query),
  get: (id: number) => apiClient.get<ResidentDetail>(`/api/residents/${id}`).then((r) => r.data),
  create: (body: ResidentForm) => apiClient.post<ResidentDetail>('/api/residents', body).then((r) => r.data),
  update: (id: number, body: ResidentForm) => apiClient.put<ResidentDetail>(`/api/residents/${id}`, body).then((r) => r.data),
  discharge: (id: number, reason: string) => apiClient.post(`/api/residents/${id}/discharge`, { reason }),

  contacts: (id: number) => apiClient.get<Contact[]>(`/api/residents/${id}/contacts`).then((r) => r.data),
  addContact: (id: number, body: ContactForm) => apiClient.post<Contact>(`/api/residents/${id}/contacts`, body).then((r) => r.data),
  updateContact: (id: number, contactId: number, body: ContactForm) =>
    apiClient.put<Contact>(`/api/residents/${id}/contacts/${contactId}`, body).then((r) => r.data),
  deleteContact: (id: number, contactId: number) => apiClient.delete(`/api/residents/${id}/contacts/${contactId}`),

  familyLinks: (id: number) => apiClient.get<FamilyLink[]>(`/api/residents/${id}/family-links`).then((r) => r.data),
  saveFamilyLink: (id: number, body: FamilyLinkForm) => apiClient.put<FamilyLink>(`/api/residents/${id}/family-links`, body).then((r) => r.data),
  deleteFamilyLink: (id: number, linkId: number) => apiClient.delete(`/api/residents/${id}/family-links/${linkId}`),

  releaseBed: (id: number) => apiClient.post(`/api/residents/${id}/release-bed`),
  bedHistory: (id: number) => apiClient.get<BedAssignment[]>(`/api/residents/${id}/bed-history`).then((r) => r.data),
  vitals: (id: number, from?: string, to?: string) =>
    apiClient.get<VitalSign[]>(`/api/residents/${id}/vitals`, { params: { from, to } }).then((r) => r.data),
  medicationLogs: (id: number) => apiClient.get<MedicationLog[]>(`/api/residents/${id}/medication-logs`).then((r) => r.data),
  subscriptions: (id: number) => apiClient.get<Subscription[]>(`/api/residents/${id}/subscriptions`).then((r) => r.data),
};
