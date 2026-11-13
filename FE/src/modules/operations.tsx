import { lazy } from 'react';
import { Route } from 'react-router-dom';
import { ScheduleOutlined, SolutionOutlined } from '@ant-design/icons';
import { ProtectedRoute } from '../auth/ProtectedRoute';
import { CLINICAL_STAFF, FRONT_DESK } from '../auth/roles';
import type { MenuEntry } from '../layouts/menu';

/**
 * MODULE VẬN HÀNH & TÀI CHÍNH — người phụ trách: Viên (docs/tasks/README.md).
 * Mọi route + mục menu của module khai báo Ở ĐÂY; không sửa App.tsx, layouts/menu.tsx.
 *
 * - operationsPublicRoutes: trang không cần đăng nhập (nằm trong PublicLayout), vd. /dat-lich-tham-quan
 * - operationsAppRoutes:    trang trong AppLayout; luôn bọc <ProtectedRoute roles={...}>
 * - operationsMenu:         mục menu bên trái (lọc theo role)
 *
 * Trang đặt ở src/pages/Operations/, import bằng lazy() để chia nhỏ bundle.
 */

// S3-V1 tiếp nhận & tham quan
const TourBooking = lazy(() => import('../pages/Operations/TourBooking'));
const Admissions = lazy(() => import('../pages/Operations/Admissions'));
// S3-V2 ca trực & phân công phụ trách
const Shifts = lazy(() => import('../pages/Operations/Shifts'));

export const operationsPublicRoutes = (
  <>
    <Route path="/dat-lich-tham-quan" element={<TourBooking />} />
  </>
);

export const operationsAppRoutes = (
  <>
    <Route path="/staff/admissions" element={<ProtectedRoute roles={FRONT_DESK}><Admissions /></ProtectedRoute>} />
    <Route path="/staff/shifts" element={<ProtectedRoute roles={CLINICAL_STAFF}><Shifts /></ProtectedRoute>} />
  </>
);

export const operationsMenu: MenuEntry[] = [
  { key: '/staff/admissions', label: 'Tiếp nhận & tham quan', icon: <SolutionOutlined />, roles: FRONT_DESK },
  { key: '/staff/shifts', label: 'Ca trực', icon: <ScheduleOutlined />, roles: CLINICAL_STAFF },
];
