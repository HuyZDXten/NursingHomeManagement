import { lazy } from 'react';
import { Route } from 'react-router-dom';
import { WarningOutlined } from '@ant-design/icons';
import { ProtectedRoute } from '../auth/ProtectedRoute';
import { CLINICAL_STAFF } from '../auth/roles';
import type { MenuEntry } from '../layouts/menu';

const Incidents = lazy(() => import('../pages/Care/Incidents'));

/**
 * MODULE CHĂM SÓC & GIA ĐÌNH — người phụ trách: Huy (docs/tasks/README.md).
 * Mọi route + mục menu của module khai báo Ở ĐÂY; không sửa App.tsx, layouts/menu.tsx.
 */
export const careAppRoutes = (
  <>
    <Route
      path="/staff/incidents"
      element={<ProtectedRoute roles={CLINICAL_STAFF}><Incidents /></ProtectedRoute>}
    />
  </>
);

export const careMenu: MenuEntry[] = [
  { key: '/staff/incidents', label: 'Sự cố', icon: <WarningOutlined />, roles: CLINICAL_STAFF },
];
