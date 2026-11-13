import type { ReactNode } from 'react';
import {
  AppstoreOutlined, CreditCardOutlined, DashboardOutlined, FileTextOutlined, GiftOutlined, HeartOutlined,
  MedicineBoxOutlined, ScheduleOutlined, TeamOutlined, UserOutlined,
} from '@ant-design/icons';
import { ADMIN_ONLY, BILLING_STAFF, CLINICAL_STAFF, FINANCE_STAFF, STAFF } from '../auth/roles';
import { careMenu } from '../modules/care';
import { operationsMenu } from '../modules/operations';
import type { Role } from '../types';

export interface MenuEntry {
  /** Đường dẫn trang, cũng là key của menu. */
  key: string;
  label: string;
  icon: ReactNode;
  /** Role thấy mục này (chỉ để ẩn/hiện; API luôn kiểm tra lại). */
  roles: Role[];
}

/** Menu phần lõi (Sprint 0–2). Module mới KHÔNG sửa mảng này mà khai báo trong src/modules/*.tsx. */
const CORE_MENU: MenuEntry[] = [
  { key: '/staff/dashboard', label: 'Tổng quan', icon: <DashboardOutlined />, roles: BILLING_STAFF },
  { key: '/staff/residents', label: 'Cư dân', icon: <TeamOutlined />, roles: STAFF },
  { key: '/staff/rooms', label: 'Phòng & giường', icon: <AppstoreOutlined />, roles: STAFF },
  { key: '/staff/care-tasks', label: 'Công việc chăm sóc', icon: <ScheduleOutlined />, roles: CLINICAL_STAFF },
  { key: '/staff/medications', label: 'Phát thuốc', icon: <MedicineBoxOutlined />, roles: CLINICAL_STAFF },
  { key: '/staff/invoices', label: 'Hóa đơn', icon: <FileTextOutlined />, roles: BILLING_STAFF },
  { key: '/staff/payments', label: 'Đối soát PayOS', icon: <CreditCardOutlined />, roles: FINANCE_STAFF },
  { key: '/staff/packages', label: 'Gói dịch vụ', icon: <GiftOutlined />, roles: STAFF },
  { key: '/staff/accounts', label: 'Tài khoản', icon: <UserOutlined />, roles: ADMIN_ONLY },
  { key: '/family', label: 'Người thân của tôi', icon: <HeartOutlined />, roles: ['Family'] },
];

export const MENU: MenuEntry[] = [...CORE_MENU, ...operationsMenu, ...careMenu];
