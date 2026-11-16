import { apiClient } from './client';
import type { LoginResponse } from '../types';
import type { FamilyInvitation, FamilyInvitationCreated, FamilyInvitationForm, InvitationLookup } from '../types/operations';

/** BL-V1, UC-08: mã mời kích hoạt tài khoản gia đình. */
export const invitationsApi = {
  list: (residentId?: number) =>
    apiClient.get<FamilyInvitation[]>('/api/family-invitations', { params: { residentId } }).then((r) => r.data),
  create: (body: FamilyInvitationForm) =>
    apiClient.post<FamilyInvitationCreated>('/api/family-invitations', body).then((r) => r.data),
  revoke: (id: number) => apiClient.post<FamilyInvitation>(`/api/family-invitations/${id}/revoke`).then((r) => r.data),
  lookup: (code: string) =>
    apiClient.get<InvitationLookup>('/api/family-invitations/lookup', { params: { code } }).then((r) => r.data),
  accept: (body: { code: string; fullName: string; phone?: string; password: string }) =>
    apiClient.post<LoginResponse>('/api/family-invitations/accept', body).then((r) => r.data),
};
