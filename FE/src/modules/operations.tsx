import { lazy } from 'react';
import { Route } from 'react-router-dom';
import {
  AppstoreAddOutlined, AuditOutlined, BarChartOutlined, BookOutlined, MessageOutlined, NotificationOutlined, PictureOutlined, ScheduleOutlined,
  SettingOutlined, ShoppingCartOutlined, SolutionOutlined, TeamOutlined, ToolOutlined, UserOutlined,
} from '@ant-design/icons';
import { ProtectedRoute } from '../auth/ProtectedRoute';
import { ADMIN_ONLY, CLINICAL_STAFF, FRONT_DESK, STAFF } from '../auth/roles';
import type { MenuEntry } from '../layouts/menu';
import type { Role } from '../types';

/** Ai có ít nhất một tab báo cáo (từng tab tự lọc role; Nurse xem báo cáo sự cố UC-125). */
const REPORT_VIEWERS: Role[] = ['Admin', 'Receptionist', 'Accountant', 'Doctor', 'Nurse'];

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
// S4-V1 AI: tri thức cho chatbot (widget công khai gắn ở PublicLayout)
const AiKnowledge = lazy(() => import('../pages/Operations/AiKnowledge'));
// BL-V1 mã mời tài khoản gia đình (bảng mời nằm trong tab "Tài khoản gia đình" của hồ sơ cư dân)
const ActivateAccount = lazy(() => import('../pages/Operations/ActivateAccount'));
// BL-V3 báo hỏng & sửa chữa, giữ giường, loại phòng & phụ thu
const RoomOps = lazy(() => import('../pages/Operations/RoomOps'));
// BL-V4 nhân sự: trang chủ cá nhân, hồ sơ & chứng chỉ & chấm công (Admin)
const StaffHome = lazy(() => import('../pages/Operations/StaffHome'));
const StaffHr = lazy(() => import('../pages/Operations/StaffHr'));
// BL-V5 hệ thống & truyền thông: nhật ký, cấu hình & danh mục, thông báo hàng loạt, tin tức & album
const AuditLogs = lazy(() => import('../pages/Operations/AuditLogs'));
const SystemSettings = lazy(() => import('../pages/Operations/SystemSettings'));
const Announcements = lazy(() => import('../pages/Operations/Announcements'));
const PostsAdmin = lazy(() => import('../pages/Operations/PostsAdmin'));
const News = lazy(() => import('../pages/Public/News'));
const NewsDetail = lazy(() => import('../pages/Public/NewsDetail'));

export const operationsPublicRoutes = (
  <>
    <Route path="/dat-lich-tham-quan" element={<TourBooking />} />
    <Route path="/kich-hoat" element={<ActivateAccount />} />
    <Route path="/tin-tuc" element={<News />} />
    <Route path="/tin-tuc/:slug" element={<NewsDetail />} />
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
    <Route path="/staff/ai-knowledge" element={<ProtectedRoute roles={ADMIN_ONLY}><AiKnowledge /></ProtectedRoute>} />
    <Route path="/staff/room-ops" element={<ProtectedRoute roles={STAFF}><RoomOps /></ProtectedRoute>} />
    <Route path="/staff/home" element={<ProtectedRoute roles={STAFF}><StaffHome /></ProtectedRoute>} />
    <Route path="/staff/hr" element={<ProtectedRoute roles={ADMIN_ONLY}><StaffHr /></ProtectedRoute>} />
    <Route path="/staff/audit" element={<ProtectedRoute roles={ADMIN_ONLY}><AuditLogs /></ProtectedRoute>} />
    <Route path="/staff/settings" element={<ProtectedRoute roles={ADMIN_ONLY}><SystemSettings /></ProtectedRoute>} />
    <Route path="/staff/announcements" element={<ProtectedRoute roles={ADMIN_ONLY}><Announcements /></ProtectedRoute>} />
    <Route path="/staff/posts" element={<ProtectedRoute roles={FRONT_DESK}><PostsAdmin /></ProtectedRoute>} />
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
  { key: '/staff/ai-knowledge', label: 'Tri thức trợ lý AI', icon: <BookOutlined />, roles: ADMIN_ONLY },
  { key: '/staff/room-ops', label: 'Vận hành phòng', icon: <ToolOutlined />, roles: STAFF },
  { key: '/staff/home', label: 'Trang chủ của tôi', icon: <UserOutlined />, roles: ['Nurse', 'Caregiver', 'Doctor'] },
  { key: '/staff/hr', label: 'Nhân sự & chấm công', icon: <TeamOutlined />, roles: ADMIN_ONLY },
  { key: '/staff/posts', label: 'Tin tức & album', icon: <PictureOutlined />, roles: FRONT_DESK },
  { key: '/staff/announcements', label: 'Thông báo hàng loạt', icon: <NotificationOutlined />, roles: ADMIN_ONLY },
  { key: '/staff/settings', label: 'Cấu hình & danh mục', icon: <SettingOutlined />, roles: ADMIN_ONLY },
  { key: '/staff/audit', label: 'Nhật ký thao tác', icon: <AuditOutlined />, roles: ADMIN_ONLY },
];
