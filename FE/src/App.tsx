import { lazy, Suspense } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { Button, Result, Spin } from 'antd';
import { useAuth } from './auth/AuthContext';
import { ProtectedRoute } from './auth/ProtectedRoute';
import {
  ADMIN_ONLY, BILLING_STAFF, CLINICAL_STAFF, FINANCE_STAFF, STAFF, homePath,
} from './auth/roles';
import { AppLayout, PublicLayout } from './layouts/AppLayout';
import { careAppRoutes } from './modules/care';
import { operationsAppRoutes, operationsPublicRoutes } from './modules/operations';

// Trang phần lõi (Sprint 0–2). Trang của module Sprint 3+ khai báo trong src/modules/*.tsx.
const PublicHome = lazy(() => import('./pages/Public/Home'));
const Login = lazy(() => import('./pages/Public/Login'));
const ForgotPassword = lazy(() => import('./pages/Public/ForgotPassword'));
const Profile = lazy(() => import('./pages/Common/Profile'));
const Dashboard = lazy(() => import('./pages/Admin/Dashboard'));
const Accounts = lazy(() => import('./pages/Admin/Accounts'));
const Packages = lazy(() => import('./pages/Admin/Packages'));
const Residents = lazy(() => import('./pages/Staff/Residents'));
const ResidentDetail = lazy(() => import('./pages/Staff/ResidentDetail'));
const Rooms = lazy(() => import('./pages/Staff/Rooms'));
const CareTasks = lazy(() => import('./pages/Staff/CareTasks'));
const Medications = lazy(() => import('./pages/Staff/Medications'));
const Invoices = lazy(() => import('./pages/Billing/Invoices'));
const InvoiceDetail = lazy(() => import('./pages/Billing/InvoiceDetail'));
const Payments = lazy(() => import('./pages/Billing/Payments'));
const FamilyHome = lazy(() => import('./pages/Family/FamilyHome'));
const FamilyResident = lazy(() => import('./pages/Family/FamilyResident'));
const FamilyInvoice = lazy(() => import('./pages/Family/FamilyInvoice'));
const PaymentResult = lazy(() => import('./pages/Family/PaymentResult'));

function RoleHome() {
  const { user } = useAuth();
  return <Navigate to={user ? homePath(user.role) : '/login'} replace />;
}

export default function App() {
  return (
    <Suspense fallback={<Spin style={{ display: 'block', margin: 48 }} />}>
    <Routes>
      {/* Công khai (UC-14..UC-18) */}
      <Route element={<PublicLayout />}>
        <Route path="/" element={<PublicHome />} />
        <Route path="/login" element={<Login />} />
        <Route path="/forgot-password" element={<ForgotPassword />} />
        {operationsPublicRoutes}
      </Route>
      <Route path="/app" element={<RoleHome />} />

      {/* Đã đăng nhập: nhân viên + gia đình dùng chung layout, menu lọc theo role */}
      <Route element={<ProtectedRoute><AppLayout /></ProtectedRoute>}>
        <Route path="/profile" element={<Profile />} />

        <Route path="/staff/dashboard" element={<ProtectedRoute roles={BILLING_STAFF}><Dashboard /></ProtectedRoute>} />
        <Route path="/staff/accounts" element={<ProtectedRoute roles={ADMIN_ONLY}><Accounts /></ProtectedRoute>} />
        <Route path="/staff/packages" element={<ProtectedRoute roles={STAFF}><Packages /></ProtectedRoute>} />
        <Route path="/staff/residents" element={<ProtectedRoute roles={STAFF}><Residents /></ProtectedRoute>} />
        <Route path="/staff/residents/:id" element={<ProtectedRoute roles={STAFF}><ResidentDetail /></ProtectedRoute>} />
        <Route path="/staff/rooms" element={<ProtectedRoute roles={STAFF}><Rooms /></ProtectedRoute>} />
        <Route path="/staff/care-tasks" element={<ProtectedRoute roles={CLINICAL_STAFF}><CareTasks /></ProtectedRoute>} />
        <Route path="/staff/medications" element={<ProtectedRoute roles={CLINICAL_STAFF}><Medications /></ProtectedRoute>} />
        <Route path="/staff/invoices" element={<ProtectedRoute roles={BILLING_STAFF}><Invoices /></ProtectedRoute>} />
        <Route path="/staff/invoices/:id" element={<ProtectedRoute roles={BILLING_STAFF}><InvoiceDetail /></ProtectedRoute>} />
        <Route path="/staff/payments" element={<ProtectedRoute roles={FINANCE_STAFF}><Payments /></ProtectedRoute>} />

        <Route path="/family" element={<ProtectedRoute roles={['Family']}><FamilyHome /></ProtectedRoute>} />
        <Route path="/family/residents/:id" element={<ProtectedRoute roles={['Family']}><FamilyResident /></ProtectedRoute>} />
        <Route path="/family/invoices/:id" element={<ProtectedRoute roles={['Family']}><FamilyInvoice /></ProtectedRoute>} />
        <Route path="/family/payments/result" element={<ProtectedRoute roles={['Family']}><PaymentResult /></ProtectedRoute>} />

        {/* Module Sprint 3+ */}
        {operationsAppRoutes}
        {careAppRoutes}

        <Route path="/forbidden" element={<Result status="403" title="Không có quyền" subTitle="Tài khoản của bạn không được truy cập trang này." extra={<Button href="/app">Về trang chính</Button>} />} />
      </Route>
      <Route path="*" element={<Result status="404" title="Không tìm thấy trang" extra={<Button href="/">Trang chủ</Button>} />} />
    </Routes>
    </Suspense>
  );
}
