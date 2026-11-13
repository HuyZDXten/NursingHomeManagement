# Hướng dẫn cho thành viên — Nursing Home Management

Tài liệu này giúp bạn chạy dự án, hiểu kiến trúc và tự thêm use case mới đúng chuẩn. Sprint 0–2 đã xong (xem [SPRINTS.md](SPRINTS.md)); Sprint 3–5 do **Viên** (module Vận hành & Tài chính) và **Huy** (module Chăm sóc & Gia đình) làm.

Thứ tự đọc khi mới vào dự án: mục 1 file này (cài máy) → [ARCHITECTURE.md](ARCHITECTURE.md) (kiến trúc, ERD, điểm mở rộng 2 module) → mục 3–5 file này (phân quyền, quy ước, ví dụ) → [tasks/README.md](tasks/README.md) (bảng task, quyền sở hữu file) → file task của bạn.

## 1. Cài đặt máy mới (15 phút)

Cần: **.NET SDK 10**, **Node.js 20+**, **SQL Server** (Windows Authentication) và **sqlcmd**.

```powershell
git clone <repo> ; cd NursingHomeManagement
git checkout develop                     # hoặc nhánh nhóm đang dùng (mục 9)

# Database (tạo NursingHomeDB + dữ liệu demo)
.\Database\setup-local-db.ps1
# SQL Server không phải localhost / dùng tài khoản sa:
.\Database\setup-local-db.ps1 -Server ".\SQLEXPRESS" -SqlUser sa -SqlPassword "..."

# API
Copy-Item BE\NursingHome.API\appsettings.Development.example.json BE\NursingHome.API\appsettings.Development.json
#   Mở file vừa tạo, đổi Jwt:Key (>= 32 ký tự ngẫu nhiên). Nếu SQL không phải localhost,
#   thêm "ConnectionStrings": { "DefaultConnection": "..." } vào file này.
cd BE
dotnet run --project NursingHome.API --launch-profile http     # http://localhost:5080/swagger
dotnet run --project NursingHome.API -- --check-db             # kiểm tra kết nối

# PayOS giả lập (cửa sổ khác): http://localhost:5199/dev
node scripts/payos-fake/server.mjs

# FE (cửa sổ khác): http://localhost:5173
cd FE; Copy-Item .env.example .env; npm install; npm run dev
```

Tài khoản demo — mật khẩu chung `Demo@123456`:

| Email | Role | Ghi chú |
| --- | --- | --- |
| admin@nursinghome.local | Admin | Toàn quyền |
| receptionist@nursinghome.local | Receptionist | Cư dân, phòng/giường, hóa đơn |
| accountant@nursinghome.local | Accountant | Hóa đơn, thu tiền, đối soát PayOS |
| doctor@nursinghome.local | Doctor | Nhập/dừng lịch thuốc |
| nurse@nursinghome.local | Nurse | Giao việc, sinh hiệu, phát thuốc; nhận cảnh báo sinh hiệu |
| caregiver@nursinghome.local | Caregiver | Chỉ thấy NH-0001, NH-0002 (đang có việc được giao) |
| caregiver2@nursinghome.local | Caregiver | Chỉ thấy NH-0004 |
| family@nursinghome.local | Family | Người thân NH-0001, đủ 4 quyền, hóa đơn trả một phần |
| family2@nursinghome.local | Family | Người thân NH-0002, không xem thuốc, hóa đơn chưa trả → thử PayOS |

**Không commit**: `appsettings.Development.json`, `FE/.env`, khóa thật (JWT, Gmail App Password, PayOS, Gemini). `.gitignore` đã chặn sẵn.

## 2. Kiến trúc

```text
React FE ──REST/OData/SignalR──> NursingHome.API ──> Services ──> Repositories ──> DataAccessObjects ──> SQL Server
                                     │                  │                                  ▲
                                     │                  ├──> payOS / SMTP / (Sprint 4: Gemini)
payOS ──webhook──────────────────────┘                  │
DTOs: dùng chung API ↔ Services        BusinessObjects: Entities, Enums, Exceptions, DbContext (mọi layer)
Database/*.sql ──sqlcmd──> SQL Server (nguồn sự thật của schema)
```

| Project | Đặt gì ở đây | KHÔNG đặt |
| --- | --- | --- |
| `NursingHome.BusinessObjects` | Entity (1 file/nhóm), enum, `DomainExceptions`, `NursingHomeContext` (mapping), `Helpers/InvoiceMath` | Logic gọi DB |
| `NursingHome.DataAccessObjects` | DAO singleton `XxxDAO.Instance`, mỗi method tự mở context ngắn hạn. Thao tác nhiều bước cần nhất quán (chuyển giường, ghi tiền) = **một method chạy trọn trong một transaction** | Kiểm tra quyền |
| `NursingHome.Repositories` | Interface + implementation 1 dòng ủy quyền cho DAO | Logic |
| `NursingHome.Services` | Nghiệp vụ, kiểm tra quyền theo cư dân, map entity→DTO (`Helpers/DtoMapper.cs`), tích hợp ngoài, đăng ký DI (`ServiceCollectionExtensions.cs`) | `HttpContext`, SignalR |
| `NursingHome.DTOs` | Request (DataAnnotations để validate) / Response, `CurrentUser` | Entity |
| `NursingHome.API` | Controller mỏng, policy, OData, middleware lỗi, SignalR hub, background job, `Program.cs`, appsettings | Nghiệp vụ |

Luồng một request: Controller lấy `User.RequireCurrentUser()` → gọi Service → Service kiểm tra quyền (`IResidentAccessService`) + quy tắc → Repository → DAO. Service ném exception trong `BusinessObjects/Exceptions`, `ExceptionHandlingMiddleware` đổi thành HTTP:

| Exception | HTTP |
| --- | --- |
| `BusinessRuleException` | 400 |
| `UnauthorizedServiceException` | 401 |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException`, `DbUpdateConcurrencyException` (rowversion), SQL 2601/2627 (unique), 547, 1205 (deadlock) | 409 |
| `TooManyRequestsException` | 429 |
| `ServiceUnavailableException` (SMTP/PayOS chưa cấu hình) | 503 |

Mọi lỗi trả `{ "message": "..." }` tiếng Việt; FE đọc bằng `extractErrorMessage(e)`.

## 3. Phân quyền

Role (cột `Accounts.Role`): `Admin, Receptionist, Accountant, Doctor, Nurse, Caregiver, Family`. Policy ở `NursingHome.API/Security/AuthPolicies.cs`, FE có bản tương ứng ở `FE/src/auth/roles.ts`:

| Policy | Role |
| --- | --- |
| `AdminOnly` | Admin |
| `Staff` | mọi role trừ Family |
| `FrontDesk` | Admin, Receptionist |
| `BillingStaff` | Admin, Receptionist, Accountant |
| `FinanceStaff` | Admin, Accountant |
| `InvoiceReaders` | Admin, Receptionist, Accountant, Family |
| `ClinicalStaff` | Admin, Doctor, Nurse, Caregiver |
| `CarePlanners` | Admin, Nurse |
| `Prescribers` | Admin, Doctor |
| `FamilyOnly` | Family |

Quyền theo từng cư dân (`ResidentAccessService`, chống IDOR) — **mọi service đọc dữ liệu cư dân phải gọi**:

| Role | Cơ bản | Sức khỏe / Chăm sóc / Thuốc | Hóa đơn |
| --- | --- | --- | --- |
| Admin | ✔ | ✔ | ✔ |
| Receptionist, Accountant | ✔ | — | ✔ |
| Doctor, Nurse | ✔ | ✔ | — |
| Caregiver | chỉ cư dân đang có việc Pending/InProgress giao cho mình | như cột trái | — |
| Family | cư dân được liên kết | theo cờ `CanViewHealth/Care/Medication` + bản ghi có `ShareWithFamily` | theo `CanViewBilling`, không thấy Draft |

> **Bẫy đã gặp:** `[Authorize(Policy=...)]` ở class **và** ở action là điều kiện **AND**. Nếu action cần mở rộng hơn class (ví dụ Family xem hóa đơn), để class chỉ `[Authorize]` và đặt policy trên từng action (xem `InvoicesController`, `AccountsController`).

Đổi role/khóa tài khoản sẽ thu hồi refresh token: người đó bị đăng xuất khi access token (30 phút) hết hạn.

## 4. Quy ước code

- **Thời gian**: DB lưu UTC (`DATETIME2`), `DateTime` đọc ra có `Kind=Utc`. "Hôm nay", giờ uống thuốc… luôn qua `FacilityClock` (UTC+7): `clock.Today`, `clock.ToUtc(date, time)`, `clock.StartOfDayUtc(date)`. FE hiển thị bằng `dateTime()/timeOf()` (múi giờ `Asia/Ho_Chi_Minh`).
- **Tiền**: VND `DECIMAL(18,0)`, làm tròn từng dòng tới đồng (`InvoiceMath`). Service từ chối số lẻ.
- **Enum**: DB lưu chuỗi + `CHECK`; EF `HasConversion<string>()`; DTO dùng `string`.
- **Không trả entity ra API**: luôn map sang DTO (thêm hàm vào `DtoMapper`).
- **Danh sách lớn** dùng OData: thêm `EntitySet` ở `OData/EdmModelBuilder.cs` + action `[EnableQuery]` ở `ODataControllers.cs`. Service trả danh sách **đã lọc theo quyền**; tên trường camelCase.
- **Chống ghi đè**: bảng hay bị sửa đồng thời có `ROWVERSION`; DTO trả `rowVersion` (base64), client gửi lại khi cập nhật (xem `CareTaskService.UpdateAsync`).
- **Ràng buộc quan trọng đặt ở DB** (unique, filtered index, CHECK) để vẫn đúng khi hai người bấm cùng lúc.
- **Thông báo**: `INotificationService.NotifyAsync(accountIds, type, title, body, link)` hoặc `NotifyRolesAsync(...)` — lưu DB rồi đẩy SignalR. `link` là đường dẫn FE.
- **Y tế**: không chẩn đoán, không kê đơn/đề xuất liều; ngưỡng cảnh báo chỉ để nhân viên xem lại.
- Tên file, class, biến tiếng Anh; chuỗi hiển thị và comment tiếng Việt; 4 dấu cách (`.editorconfig`).

## 5. Thêm một use case — ví dụ UC-58 "Báo cáo sự cố"

> Ví dụ rút gọn để hiểu khuôn mẫu. Bản đầy đủ (đúng tên cột, quy tắc, kiểm thử) là task [S3-H1](tasks/S3-H1-su-co.md). Từ Sprint 3, code mới đặt vào **file của module mình** (bảng ở [ARCHITECTURE.md](ARCHITECTURE.md) mục 6), không sửa file lõi.

**Bước 1 — Database** (`Database/01_Schema.sql`, chèn trong **vùng của module mình** — mục 9 Viên, mục 10 Huy — theo đúng kiểu `IF OBJECT_ID ... IS NULL`):

```sql
IF OBJECT_ID(N'dbo.Incidents', N'U') IS NULL
CREATE TABLE dbo.Incidents (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Incidents PRIMARY KEY,
    ResidentId   INT            NOT NULL CONSTRAINT FK_Incidents_Residents REFERENCES dbo.Residents (Id),
    Type         NVARCHAR(30)   NOT NULL,
    Severity     NVARCHAR(20)   NOT NULL,
    Description  NVARCHAR(2000) NOT NULL,
    OccurredAt   DATETIME2      NOT NULL,
    ReportedById INT            NOT NULL CONSTRAINT FK_Incidents_Accounts REFERENCES dbo.Accounts (Id),
    Status       NVARCHAR(20)   NOT NULL CONSTRAINT DF_Incidents_Status DEFAULT (N'Open'),
    Resolution   NVARCHAR(2000) NULL,
    CreatedAt    DATETIME2      NOT NULL CONSTRAINT DF_Incidents_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_Incidents_Type CHECK (Type IN (N'Fall', N'Injury', N'Behavior', N'Other')),
    CONSTRAINT CK_Incidents_Severity CHECK (Severity IN (N'Low', N'Medium', N'High')),
    CONSTRAINT CK_Incidents_Status CHECK (Status IN (N'Open', N'Closed'))
);
GO
```

Thêm 1–2 dòng demo vào vùng của mình trong `05_DemoSeed.sql`. Bảng mới chỉ cần chạy `.\Database\setup-local-db.ps1` (không `-Recreate`): bảng được tạo, dữ liệu cũ giữ nguyên. Chỉ khi **sửa bảng đã có** mới cần thêm `Database/upgrades/yyyy-MM-dd_TenThayDoi.sql`.

**Bước 2 — BusinessObjects**: enum `IncidentStatus { Open, Closed }` và entity `Incident` vào file mới `Entities/CareIncidents.cs` (không sửa `Enums.cs`); trong `NursingHomeContext.Care.cs`: `public DbSet<Incident> Incidents => Set<Incident>();` và trong `ConfigureCare(...)`:

```csharp
modelBuilder.Entity<Incident>(entity =>
{
    entity.ToTable("Incidents");
    entity.Property(e => e.Status).HasConversion<string>();
    entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
});
```

**Bước 3 — DAO** (file mới `IncidentDAO.cs`):

```csharp
public sealed class IncidentDAO : GenericDAO<Incident>
{
    public static IncidentDAO Instance { get; } = new();
    private IncidentDAO() { }

    public async Task<List<Incident>> ListAsync(int? residentId, bool openOnly)
    {
        await using var context = CreateContext();
        return await context.Incidents.AsNoTracking().Include(i => i.Resident)
            .Where(i => (residentId == null || i.ResidentId == residentId) && (!openOnly || i.Status == IncidentStatus.Open))
            .OrderByDescending(i => i.OccurredAt).ToListAsync();
    }
}
```

**Bước 4 — Repository**: `IIncidentRepository` + `IncidentRepository` (mỗi method một dòng gọi `IncidentDAO.Instance`).

**Bước 5 — DTOs** (`DTOs/Care/IncidentDtos.cs`): `IncidentDto`, `ReportIncidentRequest` (có `[Required]`, `[MaxLength]`), `CloseIncidentRequest`.

**Bước 6 — Service** (`IIncidentService` trong `Interfaces/ICareModuleServices.cs`, implementation mới; map DTO trong `CareMapper` ở `Helpers/ModuleMappers.cs`):

```csharp
public async Task<IncidentDto> ReportAsync(CurrentUser user, ReportIncidentRequest request)
{
    await access.RequireAsync(user, request.ResidentId, ResidentSection.Care);   // luôn kiểm quyền theo cư dân
    var incident = await incidents.AddAsync(new Incident { ..., ReportedById = user.AccountId, OccurredAt = clock.UtcNow });
    await notifications.NotifyRolesAsync([Roles.Nurse, Roles.Admin], NotificationType.Care,
        "Có sự cố mới", request.Description, $"/staff/residents/{request.ResidentId}");
    return incident.ToDto();
}
```

Đăng ký 2 dòng trong `ServiceCollectionExtensions.Care.cs` → `AddCareModule(...)` (repository + service).

**Bước 7 — Controller** (`api/incidents`, policy `ClinicalStaff`; đóng sự cố chỉ `CarePlanners`). Danh sách cần OData thì khai báo entity set trong `OData/EdmModelBuilder.Care.cs`.

**Bước 8 — FE**: kiểu ở `types/care.ts`, hàm ở `api/incidents.ts`, trang ở `pages/Care/Incidents.tsx`, route + mục menu trong `src/modules/care.tsx` (không sửa `App.tsx`, `layouts/menu.tsx`), nhãn trạng thái trong `utils/status.care.ts`. Tab trong hồ sơ cư dân: thêm vào `pages/Staff/resident/ClinicalTabs.tsx`.

**Bước 9 — Kiểm tra**: unit test cho quy tắc thuần (nếu có); thêm vài `check(...)` vào `scripts/smoke-test.mjs`; thử bằng tài khoản được phép **và không được phép**; cập nhật `docs/SPRINTS.md`.

## 6. Thanh toán PayOS

Mô hình từ StreetBiz, rút gọn cho **một** người nhận tiền là cơ sở:

1. `POST /api/invoices/{id}/payos/checkout` (Family có quyền Billing, hoặc lễ tân/kế toán): tạo `PaymentTransactions` với số tiền còn nợ; `orderCode = transactionId × 1000 + attempt`; gọi payOS `POST /v2/payment-requests` (ký HMAC-SHA256). Link còn hạn và đúng số tiền thì trả lại đúng QR cũ.
2. FE vẽ VietQR (`PayOsQrPanel`), poll `GET /api/payments/transactions/{id}` mỗi 5 giây.
3. **Lớp 1** — webhook `POST /api/payments/payos/webhook` (ẩn danh, kiểm chữ ký bằng `ChecksumKey`, luôn trả 200).
   **Lớp 2** — poll của FE và `PayOsSyncService` (mỗi `SyncIntervalSeconds`) hỏi payOS `GET /v2/payment-requests/{orderCode}`. Mất webhook vẫn ghi nhận được.
4. Cả hai đi qua `BillingDAO.ApplyPaymentAsync` (Serializable): `Payments.Reference = "PAYOS-{orderCode}"` là UNIQUE nên không ghi hai lần; hóa đơn chuyển `PartiallyPaid/Paid`; báo Family + Accountant.
5. Mọi báo có ghi vào `PaymentWebhookLogs`: `APPLIED`, `DUPLICATE`, `UNMATCHED`, `REJECTED` (sai chữ ký/sai số tiền), `SURPLUS` (hóa đơn đã trả/hủy — hoàn tiền tay), `IGNORED` (webhook thử của payOS, orderCode < 1000). Kế toán xem ở màn **Đối soát PayOS**.

**Local**: `node scripts/payos-fake/server.mjs` → mở `http://localhost:5199/dev`, bấm "Khách đã trả" để payOS giả gửi webhook ký đúng chuẩn; bấm "Tắt webhook" để thử lớp 2. Khóa demo đã có sẵn trong `appsettings.Development.example.json`.

**payOS thật**: tạo kênh tại payos.vn, đặt `PayOs:ApiBaseUrl=https://api-merchant.payos.vn` và 3 khóa vào `dotnet user-secrets` (project API có `UserSecretsId`):

```powershell
cd BE\NursingHome.API
dotnet user-secrets set "PayOs:ClientId" "..."
dotnet user-secrets set "PayOs:ApiKey" "..."
dotnet user-secrets set "PayOs:ChecksumKey" "..."
```

Webhook cần URL công khai (ngrok hoặc VS Dev Tunnels → `https://<tunnel>/api/payments/payos/webhook`) đăng ký trên trang payOS; không có thì lớp 2 vẫn xác nhận được.

## 7. Kiểm thử

| Loại | Lệnh | Ghi chú |
| --- | --- | --- |
| Unit | `dotnet test BE/NursingHome.sln` | 11 test: tính phí theo ngày, chữ ký PayOS, BCrypt, OTP, giờ VN, ngưỡng sinh hiệu |
| End-to-end | `.\scripts\run-smoke-isolated.ps1` | **Nên dùng**: tự dựng DB `NursingHomeDB_Test`, chạy API (5081) + PayOS giả (5198) riêng rồi chạy `smoke-test.mjs`; không đụng DB/API dev đang mở |
| End-to-end (DB dev) | `node scripts/smoke-test.mjs` | Chạy trên DB/API dev (5080) — sẽ thay đổi dữ liệu; điều kiện ở đầu file |
| FE | `npm run build`, `npm run lint` | TypeScript strict |

Thử tay mỗi tính năng bằng ít nhất 2 role: một role được phép, một role bị chặn (phải ra 403/404, không lộ dữ liệu).

## 8. Đã làm và chưa làm

Chi tiết từng use case ở [SPRINTS.md](SPRINTS.md). Những điểm cần biết:

- Email mới gửi OTP quên mật khẩu; phát hành hóa đơn mới chỉ báo in-app (email: Sprint 3, UC-118).
- `Cloudinary`, `Google`, `Ai` đã có section trong `appsettings.json` nhưng **chưa nối code** (Sprint 3–4).
- Upload ảnh cư dân/giấy tờ chưa có (UC-24 ảnh, UC-29).
- Giao diện đã build và kiểm tra qua HTTP (CORS, SignalR, các module Vite), **chưa QA bằng mắt** trên trình duyệt cho từng role — làm ở Sprint 5.
- Code MVC cũ (Razor) đã bỏ khỏi nhánh này, vẫn còn ở `main`: xem lại bằng `git show main:BE/Services/BillingService.cs`.

## 9. Quy trình Git

```text
main     ← chỉ bản đã kiểm thử để nộp/demo
develop  ← nhánh chung của nhóm
feature/<task-id>-<ten>     vd. feature/s3-h1-incidents, feature/s3-v2-shifts, feature/s4-v1-ai-core
```

```powershell
git checkout develop; git pull
git checkout -b feature/s3-h1-incidents
# ... code, build, test ...
git add -A
git commit -m "feat(incidents): UC-58 bao cao su co, UC-59 dong su co"
git push -u origin feature/s3-h1-incidents   # rồi mở Pull Request vào develop, người kia review
```

- Commit nhỏ, mỗi commit build được. Tiền tố: `feat`, `fix`, `refactor`, `docs`, `test`, `db`.
- Thay đổi DB: commit **cùng lúc** `01_Schema.sql`/`05_DemoSeed.sql` + Entity + mapping.
- Mỗi người chỉ chèn trong vùng của mình ở `01_Schema.sql` (mục 9 Viên, mục 10 Huy) và `05_DemoSeed.sql` (mục 10 Viên, mục 11 Huy), nên git thường tự gộp. Dữ liệu demo thêm vào **bảng dùng chung** (Accounts, Residents, CareTasks…) dùng dải Id riêng: **Viên 100–499, Huy 500–899**.
- Review chéo bắt buộc: người review chạy `setup-local-db.ps1 -Recreate` + smoke test trước khi approve. Chi tiết: [tasks/README.md](tasks/README.md#quy-trình-làm-một-task).
- PR checklist: build 0 cảnh báo, test xanh, `setup-local-db.ps1 -Recreate` chạy được, đã thử bằng role bị chặn, cập nhật cột Trạng thái ở `docs/tasks/README.md`.

## 10. Lỗi thường gặp

| Hiện tượng | Cách xử lý |
| --- | --- |
| API dừng ngay: `Jwt:Key phải dài ít nhất 32 byte` | Chưa tạo `appsettings.Development.json` từ file example, hoặc key quá ngắn |
| `Không kết nối được SQL Server` | Chạy `setup-local-db.ps1`; sửa `ConnectionStrings:DefaultConnection` trong `appsettings.Development.json` |
| `sqlcmd` không có | Cài *Microsoft Command Line Utilities for SQL Server* |
| Đăng nhập trả 429 | Rate limit 10 lần/phút/IP; chờ 1 phút hoặc đặt `RateLimit__CredentialsPerMinute` khi chạy test |
| FE báo "Không kết nối được máy chủ" | API chưa chạy hoặc `FE/.env` sai `VITE_API_BASE_URL` |
| Quên mật khẩu trả 503 | Chưa cấu hình `Email:SmtpUser/SmtpPassword` (Gmail App Password) |
| Tạo VietQR trả 503 | `PayOs:Enabled=false` hoặc thiếu khóa; chạy payos-fake với khóa demo |
| payos-fake báo "Đơn thanh toán đã tồn tại" | Đã dựng lại DB (Id giao dịch bắt đầu lại từ 1): khởi động lại payos-fake |
| Caregiver không thấy cư dân | Đúng thiết kế: chỉ thấy cư dân đang có việc Pending/InProgress giao cho mình |
| Tiếng Việt hiện `?` trong `sqlcmd` | Chỉ là console; dữ liệu đúng. Luôn chạy script với `-f 65001` (setup script đã có) |
