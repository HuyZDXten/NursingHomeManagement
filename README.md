# Nursing Home Management — PRN232 Final Project

Hệ thống quản lý viện dưỡng lão: hồ sơ cư dân, phòng/giường, chăm sóc hằng ngày, sinh hiệu, lịch thuốc, hóa đơn, thanh toán **PayOS VietQR**, cổng gia đình và thông báo realtime. Kiến trúc theo các assignment PRN232 (ASS2): **ASP.NET Core Web API 6 layer + React (Vite + TypeScript)**, **database-first** bằng script SQL.

| Phần | Công nghệ |
| --- | --- |
| BE | .NET 10, ASP.NET Core Web API, EF Core 10 (chỉ mapping), OData 9, JWT + refresh token, SignalR, MailKit, BCrypt |
| FE | React 19, Vite 8, TypeScript, Ant Design 6, React Router 7, axios, Recharts, @microsoft/signalr |
| DB | SQL Server, script trong `Database/` (không dùng EF Migrations) |
| Thanh toán | payOS merchant API v2 (theo mô hình StreetBiz), có server giả lập cho local |

## Chạy nhanh (máy local)

Cần: .NET SDK 10, Node.js 20+, SQL Server (Windows Authentication) và `sqlcmd`.

```powershell
# 1. Database
.\Database\setup-local-db.ps1                 # thêm -Recreate để dựng lại từ đầu

# 2. API  (http://localhost:5080, Swagger: /swagger)
Copy-Item BE\NursingHome.API\appsettings.Development.example.json BE\NursingHome.API\appsettings.Development.json
#    -> mở file vừa tạo, đổi Jwt:Key thành chuỗi ngẫu nhiên >= 32 ký tự
cd BE; dotnet run --project NursingHome.API --launch-profile http

# 3. PayOS giả lập (cửa sổ khác) — http://localhost:5199/dev
node scripts/payos-fake/server.mjs

# 4. FE (cửa sổ khác) — http://localhost:5173
cd FE; Copy-Item .env.example .env; npm install; npm run dev
```

Tài khoản demo (mật khẩu `Demo@123456`): `admin`, `receptionist`, `accountant`, `doctor`, `nurse`, `caregiver`, `caregiver2`, `family`, `family2` — tất cả `@nursinghome.local`. Trang đăng nhập (chế độ dev) có nút điền nhanh.

## Cấu trúc

```text
BE/                         NursingHome.sln (6 project + Tests)
  NursingHome.API/          Controllers, OData, SignalR Hub, Middleware, BackgroundJobs, appsettings
  NursingHome.BusinessObjects/  Entities, Enums, Exceptions, NursingHomeContext (mapping)
  NursingHome.DataAccessObjects/ GenericDAO + DAO singleton (transaction nằm trọn trong DAO)
  NursingHome.DTOs/         Request/Response theo module
  NursingHome.Repositories/ Interfaces + Implementations (ủy quyền cho DAO)
  NursingHome.Services/     Nghiệp vụ, phân quyền theo cư dân, PayOS, email, thông báo
  NursingHome.Tests/        xUnit
FE/                         Vite + React + TS (api/, auth/, components/, layouts/, pages/<nhóm>/)
Database/                   00..05 *.sql + setup-local-db.ps1 + upgrades/
scripts/                    payos-fake (PayOS giả lập), smoke-test.mjs (kiểm thử end-to-end)
docs/                       SPRINTS.md (kế hoạch Scrum), HANDOVER.md (hướng dẫn cho thành viên)
```

## Tài liệu

- [docs/HANDOVER.md](docs/HANDOVER.md) — **đọc trước khi code**: cài máy, phân quyền, quy ước, cách thêm một chức năng, PayOS, Git.
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — kiến trúc, lần theo một request, ERD, luồng JWT/PayOS, danh mục API, điểm mở rộng 2 module.
- [docs/tasks/README.md](docs/tasks/README.md) — **bảng task Scrum** của Viên & Huy (Sprint 3–5), mỗi task một file hướng dẫn chi tiết; [BACKLOG.md](docs/tasks/BACKLOG.md) cho việc dự phòng.
- [docs/SPRINTS.md](docs/SPRINTS.md) — trạng thái 142 use case theo sprint, Definition of Done.
- [Database/README.md](Database/README.md) — quy tắc cập nhật schema và seed.

## Kiểm thử

```powershell
dotnet test BE/NursingHome.sln          # unit test quy tắc nghiệp vụ (xUnit)
.\scripts\run-smoke-isolated.ps1       # kiểm tra end-to-end trên DB/cổng riêng, không đụng bản dev đang chạy
cd FE; npm run build; npm run lint
```

> Dữ liệu sức khỏe và thuốc trong demo là **mô phỏng**. Hệ thống không chẩn đoán, không kê đơn, không đề xuất liều.
