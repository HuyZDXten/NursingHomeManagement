import type { Role } from '../types';

// Nhóm quyền khớp AuthPolicies ở API (NursingHome.API/Security/AuthPolicies.cs).
// FE chỉ dùng để ẩn/hiện menu, nút; API luôn kiểm tra lại.
export const STAFF: Role[] = ['Admin', 'Receptionist', 'Accountant', 'Doctor', 'Nurse', 'Caregiver'];
export const FRONT_DESK: Role[] = ['Admin', 'Receptionist'];
export const BILLING_STAFF: Role[] = ['Admin', 'Receptionist', 'Accountant'];
export const FINANCE_STAFF: Role[] = ['Admin', 'Accountant'];
export const CLINICAL_STAFF: Role[] = ['Admin', 'Doctor', 'Nurse', 'Caregiver'];
export const CARE_PLANNERS: Role[] = ['Admin', 'Nurse'];
export const PRESCRIBERS: Role[] = ['Admin', 'Doctor'];
export const ADMIN_ONLY: Role[] = ['Admin'];

export const hasRole = (role: Role | undefined, allowed: Role[]) => !!role && allowed.includes(role);

/** Trang mặc định sau khi đăng nhập. */
export function homePath(role: Role): string {
  switch (role) {
    case 'Family':
      return '/family';
    case 'Caregiver':
    case 'Nurse':
      return '/staff/care-tasks';
    case 'Doctor':
      return '/staff/residents';
    default:
      return '/staff/dashboard';
  }
}
