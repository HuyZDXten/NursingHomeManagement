import type { MenuEntry } from '../layouts/menu';

/**
 * MODULE CHĂM SÓC & GIA ĐÌNH — người phụ trách: Huy (docs/tasks/README.md).
 * Mọi route + mục menu của module khai báo Ở ĐÂY; không sửa App.tsx, layouts/menu.tsx.
 *
 * - careAppRoutes: trang trong AppLayout (nhân viên và gia đình); luôn bọc <ProtectedRoute roles={...}>
 * - careMenu:      mục menu bên trái (lọc theo role)
 *
 * Trang đặt ở src/pages/Care/ (nhân viên) và src/pages/Family/ (gia đình), import bằng lazy(). Ví dụ (S3-H1):
 *
 *   import { lazy } from 'react';
 *   import { Route } from 'react-router-dom';
 *   import { WarningOutlined } from '@ant-design/icons';
 *   import { ProtectedRoute } from '../auth/ProtectedRoute';
 *   import { CLINICAL_STAFF } from '../auth/roles';
 *   const Incidents = lazy(() => import('../pages/Care/Incidents'));
 *
 *   export const careAppRoutes = (
 *     <>
 *       <Route path="/staff/incidents" element={<ProtectedRoute roles={CLINICAL_STAFF}><Incidents /></ProtectedRoute>} />
 *     </>
 *   );
 *   export const careMenu: MenuEntry[] = [
 *     { key: '/staff/incidents', label: 'Sự cố', icon: <WarningOutlined />, roles: CLINICAL_STAFF },
 *   ];
 */
export const careAppRoutes = <></>;

export const careMenu: MenuEntry[] = [];
