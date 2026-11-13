# FE — Nursing Home (Vite + React + TypeScript + Ant Design)

```powershell
Copy-Item .env.example .env     # VITE_API_BASE_URL=http://localhost:5080
npm install
npm run dev                     # http://localhost:5173 (khớp Cors:AllowedOrigins của API)
npm run build                   # tsc -b && vite build
npm run lint                    # oxlint
```

## Cấu trúc `src/`

| Thư mục | Nội dung |
| --- | --- |
| `api/` | `client.ts` (axios + tự refresh JWT như ASS2), `odata.ts` (gọi entity set có `$count`), mỗi module một file: `auth`, `accounts`, `residents`, `rooms`, `care`, `billing`, `portal` |
| `auth/` | `AuthContext` (giải mã JWT), `ProtectedRoute`, `roles.ts` (nhóm quyền khớp `AuthPolicies` ở API, `homePath`) |
| `components/` | `Common.tsx` (StatusTag, PageTitle, ResidentSelect, AccountSelect), `CareTaskModals`, `VitalsChart`, `PayOsQrPanel` |
| `hooks/` | `useNotifications` (danh sách + SignalR `/hubs/notifications`) |
| `layouts/` | `AppLayout` (menu theo role — mảng `MENU`), `PublicLayout` |
| `pages/` | `Public/`, `Common/`, `Admin/`, `Staff/` (+ `resident/` các tab hồ sơ), `Billing/`, `Family/` |
| `types/` | Kiểu dữ liệu khớp DTO của API |
| `utils/` | `format.ts` (tiền VND, ngày giờ theo `Asia/Ho_Chi_Minh`), `status.ts` (nhãn + màu trạng thái, nhãn role) |

## Thêm một màn hình

1. Thêm kiểu vào `types/index.ts` và hàm gọi API vào `api/<module>.ts`.
2. Tạo trang trong `pages/<nhóm>/`.
3. Thêm `<Route>` trong `App.tsx` (bọc `ProtectedRoute roles={...}`) và một dòng trong `MENU` của `layouts/AppLayout.tsx`.
4. Thông báo lỗi luôn qua `extractErrorMessage(e)`; thông báo UI dùng `App.useApp()` của antd.

Danh sách dùng OData: tên trường **camelCase** (`$filter=status eq 'Active'`, `$orderby=code`). Thời gian từ API là UTC (`...Z`) — luôn hiển thị qua `dateTime()`/`timeOf()`.
