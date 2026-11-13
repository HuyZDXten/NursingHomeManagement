import { lazy } from 'react';
import { Route } from 'react-router-dom';
import { AppstoreAddOutlined, BarChartOutlined, MessageOutlined, ScheduleOutlined, ShoppingCartOutlined, SolutionOutlined } from '@ant-design/icons';
import { ProtectedRoute } from '../auth/ProtectedRoute';
import { ADMIN_ONLY, CLINICAL_STAFF, FRONT_DESK } from '../auth/roles';
import type { MenuEntry } from '../layouts/menu';
import type { Role } from '../types';

/** Ai có ít nhất một tab báo cáo (từng tab tự lọc role; thêm 'Nurse' khi có báo cáo sự cố UC-125 sau S3-H1). */
const REPORT_VIEWERS: Role[] = ['Admin', 'Receptionist', 'Accountant', 'Doctor'];

/** S3-V5: ai ghi được dịch vụ phát sinh (policy ServiceRecorders). */
const SERVICE_RECORDERS: Role[] = ['Admin', 'Receptionist', 'Accountant', 'Nurse', 'Caregiver'];

/** S3-V6: nhân viên dùng tin nhắn với gia đình (Caregiver chỉ thấy cư dân mình phụ trách chính). */
const CHAT_STAFF: Role[] = ['Admin', 'Receptionist', 'Nurse', 'Caregiver'];

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
// S3-V3 báo cáo
const Reports = lazy(() => import('../pages/Operations/Reports'));
// S3-V5 dịch vụ phát sinh
const ExtraServices = lazy(() => import('../pages/Operations/ExtraServices'));
const ServiceUsages = lazy(() => import('../pages/Operations/ServiceUsages'));
// S3-V6 chat gia đình – nhân viên
const Messages = lazy(() => import('../pages/Operations/Messages'));

export const operationsPublicRoutes = (
  <>
    <Route path="/dat-lich-tham-quan" element={<TourBooking />} />
  </>
);

export const operationsAppRoutes = (
  <>
    <Route path="/staff/admissions" element={<ProtectedRoute roles={FRONT_DESK}><Admissions /></ProtectedRoute>} />
    <Route path="/staff/shifts" element={<ProtectedRoute roles={CLINICAL_STAFF}><Shifts /></ProtectedRoute>} />
    <Route path="/staff/reports" element={<ProtectedRoute roles={REPORT_VIEWERS}><Reports /></ProtectedRoute>} />
    <Route path="/staff/extra-services" element={<ProtectedRoute roles={ADMIN_ONLY}><ExtraServices /></ProtectedRoute>} />
    <Route path="/staff/service-usages" element={<ProtectedRoute roles={SERVICE_RECORDERS}><ServiceUsages /></ProtectedRoute>} />
    <Route path="/staff/messages" element={<ProtectedRoute roles={CHAT_STAFF}><Messages /></ProtectedRoute>} />
    <Route path="/family/messages" element={<ProtectedRoute roles={['Family']}><Messages /></ProtectedRoute>} />
  </>
);

export const operationsMenu: MenuEntry[] = [
  { key: '/staff/admissions', label: 'Tiếp nhận & tham quan', icon: <SolutionOutlined />, roles: FRONT_DESK },
  { key: '/staff/shifts', label: 'Ca trực', icon: <ScheduleOutlined />, roles: CLINICAL_STAFF },
  { key: '/staff/reports', label: 'Báo cáo', icon: <BarChartOutlined />, roles: REPORT_VIEWERS },
  { key: '/staff/service-usages', label: 'Ghi dịch vụ phát sinh', icon: <ShoppingCartOutlined />, roles: SERVICE_RECORDERS },
  { key: '/staff/extra-services', label: 'Dịch vụ phát sinh', icon: <AppstoreAddOutlined />, roles: ADMIN_ONLY },
  { key: '/staff/messages', label: 'Tin nhắn gia đình', icon: <MessageOutlined />, roles: CHAT_STAFF },
  { key: '/family/messages', label: 'Tin nhắn với cơ sở', icon: <MessageOutlined />, roles: ['Family'] },
];
