import type { MenuEntry } from '../layouts/menu';

/**
 * MODULE VẬN HÀNH & TÀI CHÍNH — người phụ trách: Viên (docs/tasks/README.md).
 * Mọi route + mục menu của module khai báo Ở ĐÂY; không sửa App.tsx, layouts/menu.tsx.
 *
 * - operationsPublicRoutes: trang không cần đăng nhập (nằm trong PublicLayout), vd. /dat-lich-tham-quan
 * - operationsAppRoutes:    trang trong AppLayout; luôn bọc <ProtectedRoute roles={...}>
 * - operationsMenu:         mục menu bên trái (lọc theo role)
 *
 * Trang đặt ở src/pages/Operations/, import bằng lazy() để chia nhỏ bundle. Ví dụ (S3-V1):
 *
 *   import { lazy } from 'react';
 *   import { Route } from 'react-router-dom';
 *   import { SolutionOutlined } from '@ant-design/icons';
 *   import { ProtectedRoute } from '../auth/ProtectedRoute';
 *   import { FRONT_DESK } from '../auth/roles';
 *   const Admissions = lazy(() => import('../pages/Operations/Admissions'));
 *
 *   export const operationsAppRoutes = (
 *     <>
 *       <Route path="/staff/admissions" element={<ProtectedRoute roles={FRONT_DESK}><Admissions /></ProtectedRoute>} />
 *     </>
 *   );
 *   export const operationsMenu: MenuEntry[] = [
 *     { key: '/staff/admissions', label: 'Tiếp nhận & tham quan', icon: <SolutionOutlined />, roles: FRONT_DESK },
 *   ];
 */
export const operationsPublicRoutes = <></>;

export const operationsAppRoutes = <></>;

export const operationsMenu: MenuEntry[] = [];
