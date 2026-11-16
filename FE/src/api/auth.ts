import { apiClient } from './client';
import type { LoginResponse, Profile } from '../types';
import type { AuthConfig } from '../types/operations';

const imageForm = (file: File) => {
  const form = new FormData();
  form.append('file', file);
  return form;
};

export const authApi = {
  /** BL-V1: Client ID Google (rỗng = ẩn nút). */
  config: () => apiClient.get<AuthConfig>('/api/auth/config').then((r) => r.data),
  google: (idToken: string) => apiClient.post<LoginResponse>('/api/auth/google', { idToken }).then((r) => r.data),
  uploadAvatar: (file: File) => apiClient.post<Profile>('/api/auth/me/avatar', imageForm(file)).then((r) => r.data),
  removeAvatar: () => apiClient.delete<Profile>('/api/auth/me/avatar').then((r) => r.data),
  login: (email: string, password: string) =>
    apiClient.post<LoginResponse>('/api/auth/login', { email, password }).then((r) => r.data),
  logout: (refreshToken: string) => apiClient.post('/api/auth/logout', { refreshToken }),
  forgotPassword: (email: string) =>
    apiClient.post<{ message: string }>('/api/auth/forgot-password', { email }).then((r) => r.data),
  resetPassword: (email: string, otp: string, newPassword: string) =>
    apiClient.post<{ message: string }>('/api/auth/reset-password', { email, otp, newPassword }).then((r) => r.data),
  changePassword: (currentPassword: string, newPassword: string) =>
    apiClient.post<LoginResponse>('/api/auth/change-password', { currentPassword, newPassword }).then((r) => r.data),
  me: () => apiClient.get<Profile>('/api/auth/me').then((r) => r.data),
  updateMe: (body: { fullName: string; phone?: string | null }) => apiClient.put<Profile>('/api/auth/me', body).then((r) => r.data),
};
