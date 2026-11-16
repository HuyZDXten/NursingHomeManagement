import { lazy } from 'react';
import { Route } from 'react-router-dom';
import {
  AuditOutlined,
  CalendarOutlined,
  CommentOutlined,
  MessageOutlined,
  RobotOutlined,
  ScheduleOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { ProtectedRoute } from '../auth/ProtectedRoute';
import { ADMIN_ONLY, CARE_PLANNERS, CLINICAL_STAFF, FRONT_DESK } from '../auth/roles';
import type { MenuEntry } from '../layouts/menu';

const Incidents = lazy(() => import('../pages/Care/Incidents'));
const Visits = lazy(() => import('../pages/Care/Visits'));
const DailyLogs = lazy(() => import('../pages/Care/DailyLogs'));
const DueAssessments = lazy(() => import('../pages/Care/DueAssessments'));
const AiDigestReview = lazy(() => import('../pages/Care/AiDigestReview'));
const Feedbacks = lazy(() => import('../pages/Care/Feedbacks'));
const FamilyVisits = lazy(() => import('../pages/Family/FamilyVisits'));
const FamilyFeedback = lazy(() => import('../pages/Family/FamilyFeedback'));

const ASSESSMENT_READERS = [...CARE_PLANNERS, 'Doctor'] as const;

/**
 * MODULE CHĂM SÓC & GIA ĐÌNH — người phụ trách: Huy (docs/tasks/README.md).
 * Mọi route + mục menu của module khai báo Ở ĐÂY; không sửa App.tsx, layouts/menu.tsx.
 */
export const careAppRoutes = (
  <>
    <Route
      path="/staff/daily-logs"
      element={<ProtectedRoute roles={CLINICAL_STAFF}><DailyLogs /></ProtectedRoute>}
    />
    <Route
      path="/staff/assessments"
      element={<ProtectedRoute roles={[...ASSESSMENT_READERS]}><DueAssessments /></ProtectedRoute>}
    />
    <Route
      path="/staff/ai-digests"
      element={<ProtectedRoute roles={CARE_PLANNERS}><AiDigestReview /></ProtectedRoute>}
    />
    <Route
      path="/staff/incidents"
      element={<ProtectedRoute roles={CLINICAL_STAFF}><Incidents /></ProtectedRoute>}
    />
    <Route
      path="/staff/visits"
      element={<ProtectedRoute roles={FRONT_DESK}><Visits /></ProtectedRoute>}
    />
    <Route
      path="/staff/feedback"
      element={<ProtectedRoute roles={ADMIN_ONLY}><Feedbacks /></ProtectedRoute>}
    />
    <Route
      path="/family/visits"
      element={<ProtectedRoute roles={['Family']}><FamilyVisits /></ProtectedRoute>}
    />
    <Route
      path="/family/feedback"
      element={<ProtectedRoute roles={['Family']}><FamilyFeedback /></ProtectedRoute>}
    />
  </>
);

export const careMenu: MenuEntry[] = [
  { key: '/staff/daily-logs', label: 'Ghi nhận hằng ngày', icon: <ScheduleOutlined />, roles: CLINICAL_STAFF },
  { key: '/staff/assessments', label: 'Đánh giá định kỳ', icon: <AuditOutlined />, roles: [...ASSESSMENT_READERS] },
  { key: '/staff/ai-digests', label: 'Duyệt nhật ký AI', icon: <RobotOutlined />, roles: CARE_PLANNERS },
  { key: '/staff/incidents', label: 'Sự cố', icon: <WarningOutlined />, roles: CLINICAL_STAFF },
  { key: '/staff/visits', label: 'Lịch thăm', icon: <CalendarOutlined />, roles: FRONT_DESK },
  { key: '/staff/feedback', label: 'Phản hồi gia đình', icon: <CommentOutlined />, roles: ADMIN_ONLY },
  { key: '/family/visits', label: 'Lịch thăm', icon: <CalendarOutlined />, roles: ['Family'] },
  { key: '/family/feedback', label: 'Góp ý', icon: <MessageOutlined />, roles: ['Family'] },
];
