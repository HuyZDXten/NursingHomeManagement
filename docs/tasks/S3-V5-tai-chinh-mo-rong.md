# S3-V5 · Dịch vụ phát sinh, hóa đơn hàng loạt, PDF, nhắc nợ, email (UC-85, 86, 88, 97, 98, 118)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Viên | Should | 8 (có thể tách 2 PR: BE trước, FE sau) | 3.2 | — | `feature/s3-v5-billing-extras` |

## Mục tiêu

Hoàn thiện vòng tài chính: ghi nhận dịch vụ phát sinh (giặt ủi, vật lý trị liệu…) và tự đưa vào hóa đơn tháng; kế toán tạo hóa đơn cho tất cả cư dân một lần; gia đình nhận email khi hóa đơn phát hành, được nhắc trước/sau hạn; tải hóa đơn PDF.

## Tiêu chí hoàn thành

- [ ] Admin quản lý danh mục dịch vụ phát sinh (tên, đơn vị, đơn giá, đang bán).
- [ ] Nhân viên (Receptionist, Nurse, Caregiver) ghi một lần sử dụng cho cư dân: dịch vụ, ngày, số lượng; **đơn giá chốt lúc ghi**.
- [ ] Tạo hóa đơn tháng (đơn lẻ và hàng loạt) tự thêm các lần sử dụng **chưa vào hóa đơn** trong kỳ thành dòng phí; lần sử dụng đã vào hóa đơn thì không sửa/xóa được.
- [ ] Hóa đơn hàng loạt: chọn kỳ + hạn → tạo Draft cho mọi cư dân có gói trong kỳ và chưa có hóa đơn; trả về danh sách đã tạo / bỏ qua kèm lý do.
- [ ] Phát hành hóa đơn → email cho tài khoản gia đình có quyền Billing (nếu SMTP đã cấu hình), ngoài thông báo in-app đang có.
- [ ] Job nhắc nợ chạy mỗi giờ: gửi một lần cho mỗi mốc **trước hạn 3 ngày**, **quá hạn**, **quá hạn 7 ngày** (in-app + email).
- [ ] Nút **Tải PDF** ở trang hóa đơn (nhân viên) và trang hóa đơn của gia đình; gia đình chỉ tải được hóa đơn của người thân có quyền Billing.

## 1. Database — `01_Schema.sql` mục 9

```sql
IF OBJECT_ID(N'dbo.ExtraServices', N'U') IS NULL
CREATE TABLE dbo.ExtraServices (
    Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ExtraServices PRIMARY KEY,
    Name      NVARCHAR(150) NOT NULL CONSTRAINT UQ_ExtraServices_Name UNIQUE,
    Unit      NVARCHAR(30)  NOT NULL,
    UnitPrice DECIMAL(18,0) NOT NULL,
    IsActive  BIT           NOT NULL CONSTRAINT DF_ExtraServices_IsActive DEFAULT (1),
    CreatedAt DATETIME2     NOT NULL CONSTRAINT DF_ExtraServices_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt DATETIME2     NULL,
    CONSTRAINT CK_ExtraServices_Price CHECK (UnitPrice >= 0)
);
GO
IF OBJECT_ID(N'dbo.ServiceUsages', N'U') IS NULL
CREATE TABLE dbo.ServiceUsages (
    Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ServiceUsages PRIMARY KEY,
    ResidentId     INT           NOT NULL CONSTRAINT FK_ServiceUsages_Residents REFERENCES dbo.Residents (Id),
    ExtraServiceId INT           NOT NULL CONSTRAINT FK_ServiceUsages_Services REFERENCES dbo.ExtraServices (Id),
    UsedOn         DATE          NOT NULL,
    Quantity       DECIMAL(9,2)  NOT NULL,
    UnitPrice      DECIMAL(18,0) NOT NULL,   -- giá chốt lúc ghi
    Note           NVARCHAR(500) NULL,
    RecordedById   INT           NOT NULL CONSTRAINT FK_ServiceUsages_Accounts REFERENCES dbo.Accounts (Id),
    InvoiceItemId  INT           NULL CONSTRAINT FK_ServiceUsages_InvoiceItems REFERENCES dbo.InvoiceItems (Id),
    CreatedAt      DATETIME2     NOT NULL CONSTRAINT DF_ServiceUsages_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_ServiceUsages_Quantity CHECK (Quantity > 0)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ServiceUsages_Resident_Uninvoiced')
    CREATE INDEX IX_ServiceUsages_Resident_Uninvoiced ON dbo.ServiceUsages (ResidentId, UsedOn) WHERE InvoiceItemId IS NULL;
GO
IF OBJECT_ID(N'dbo.InvoiceReminders', N'U') IS NULL
CREATE TABLE dbo.InvoiceReminders (
    Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InvoiceReminders PRIMARY KEY,
    InvoiceId INT          NOT NULL CONSTRAINT FK_InvoiceReminders_Invoices REFERENCES dbo.Invoices (Id),
    Stage     NVARCHAR(20) NOT NULL,
    SentAt    DATETIME2    NOT NULL CONSTRAINT DF_InvoiceReminders_SentAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_InvoiceReminders_Stage CHECK (Stage IN (N'Before3Days', N'Overdue', N'Overdue7')),
    CONSTRAINT UQ_InvoiceReminders UNIQUE (InvoiceId, Stage)    -- mỗi mốc chỉ nhắc một lần
);
GO
```

Danh mục mẫu vào `04_ReferenceData.sql`: Giặt ủi (kg, 30.000), Vật lý trị liệu (buổi, 250.000), Cắt tóc (lần, 50.000), Đưa đón khám ngoài (chuyến, 300.000).

## 2. Backend

| Layer | File | Nội dung |
| --- | --- | --- |
| BusinessObjects | `Entities/OperationsBilling.cs` | `ExtraService`, `ServiceUsage`, `InvoiceReminder` |
| DAO | `ExtraServiceDAO.cs` | CRUD danh mục, `AddUsageAsync`, `ListUsagesAsync(residentId, from, to)`, `DeleteUsageAsync` (chỉ khi `InvoiceItemId IS NULL`) |
| DAO (lõi, Viên sở hữu) | `BillingDAO.CreateInvoiceAsync` | **Trong cùng transaction**: lấy usages chưa vào hóa đơn có `UsedOn` trong kỳ → thêm `InvoiceItem` (mô tả "Tên dịch vụ dd/MM", SL, đơn giá chốt) → `SaveChanges` → gán `InvoiceItemId`. Chuyển `CreateInvoiceAsync` sang transaction Serializable |
| | `BillingDAO.ListBillableResidentIdsAsync(period)` | cư dân có subscription giao với kỳ và chưa có hóa đơn kỳ đó |
| | `ReminderDAO.cs` | `ListDueForReminderAsync(today)` trả (invoice, stage) chưa có trong `InvoiceReminders`; `MarkSentAsync` |
| Service | `ExtraServiceService.cs`, mở rộng `BillingService` (`CreateBatchAsync`, `IssueAsync` gửi email) | Quy tắc bên dưới |
| PDF | `Services/Helpers/InvoicePdf.cs` | Package **QuestPDF** (giấy phép Community miễn phí cho đồ án; đặt `QuestPDF.Settings.License = LicenseType.Community` một lần lúc khởi động). Nội dung: tên cơ sở + địa chỉ (`FacilityOptions`), số hóa đơn, cư dân, kỳ, bảng dòng phí, tổng / đã thu / còn nợ, hướng dẫn thanh toán VietQR |
| Job | `API/BackgroundJobs/InvoiceReminderJob.cs` | `PeriodicTimer` 1 giờ, gọi `IBillingService.SendRemindersAsync()` |
| Repository lõi | `IResidentRepository` | thêm `FamilyEmailsAsync(residentId, requireBilling)` (lấy email Account Family đang hoạt động) |

Quy tắc:

- Ghi lần sử dụng: cư dân Active; dịch vụ đang bán; `Quantity` tối đa 2 chữ số thập phân; `UsedOn` không ở tương lai.
- Batch: mỗi cư dân tạo trong try/catch riêng; lỗi một người không dừng cả lô; trả `BatchInvoiceResultDto {created: [...], skipped: [{residentCode, reason}]}`.
- Email phát hành/nhắc nợ: chỉ khi `IEmailService.IsConfigured`; lỗi gửi chỉ log. Nội dung có link `${Email:FrontendBaseUrl}/family/invoices/{id}`.
- Mốc nhắc theo `clock.Today` so với `DueDate`: `Before3Days` khi còn 0–3 ngày, `Overdue` khi quá 1–6 ngày, `Overdue7` khi quá ≥ 7 ngày; chỉ hóa đơn `Unpaid/PartiallyPaid`.

## 3. API

| Method | Đường dẫn | Policy |
| --- | --- | --- |
| GET | `/api/extra-services?activeOnly=` | Staff |
| POST · PUT | `/api/extra-services[/{id}]` | AdminOnly |
| GET | `/api/residents/{id}/service-usages?from=&to=` | BillingStaff + ClinicalStaff (policy mới `ServiceRecorders`: Admin, Receptionist, Accountant, Nurse, Caregiver — PR lõi nhỏ) |
| POST | `/api/residents/{id}/service-usages` | ServiceRecorders |
| DELETE | `/api/service-usages/{id}` | ServiceRecorders (chỉ khi chưa vào hóa đơn) |
| POST | `/api/invoices/batch` `{billingPeriod, dueDate}` | BillingStaff |
| GET | `/api/invoices/{id}/pdf` | InvoiceReaders (Family kiểm như `GetInvoiceAsync`) |

## 4. Frontend

- `src/pages/Operations/ExtraServices.tsx` (Admin): CRUD như `pages/Admin/Packages.tsx`. Route `/staff/extra-services`, menu "Dịch vụ phát sinh".
- `pages/Staff/resident/Tabs.tsx` → `BillingTab` (file của Viên): thêm khối "Dịch vụ phát sinh" — bảng lần sử dụng tháng này + nút "Ghi sử dụng".
- Nurse/Caregiver không thấy tab Billing, và `ClinicalTabs.tsx` là file của Huy → tạo trang nhanh `/staff/service-usages` (chọn cư dân + dịch vụ + số lượng) trong module của Viên.
- `pages/Billing/Invoices.tsx`: nút "Tạo hóa đơn hàng loạt" → modal kỳ + hạn → hiển thị kết quả (bảng đã tạo / bỏ qua).
- `pages/Billing/InvoiceDetail.tsx` và `pages/Family/FamilyInvoice.tsx`: nút "Tải PDF" (`responseType: 'blob'`).

## 5. Dữ liệu demo

4 dịch vụ (`04`), 3 lần sử dụng tháng này cho NH-0001, NH-0003 (chưa vào hóa đơn) ở mục 10.

## 6. Kiểm thử

1. `caregiver` ghi "Giặt ủi 2 kg" cho NH-0001 → `accountant` tạo hóa đơn tháng này cho NH-0001 → có dòng "Giặt ủi"; xóa lần sử dụng đó → bị chặn.
2. Hóa đơn hàng loạt tháng sau → tạo cho NH-0001..0004, bỏ qua NH-0005 (đã ngừng lưu trú).
3. Sửa `DueDate` một hóa đơn thành hôm nay + 2 ngày, chạy `SendRemindersAsync` hai lần → `InvoiceReminders` chỉ có 1 dòng `Before3Days`.
4. `family2` tải PDF hóa đơn của NH-0002 → OK; đổi id sang hóa đơn NH-0001 → 403.

## Tham khảo code có sẵn

`BillingService.CreateInvoiceAsync`, `InvoiceMath.ProrateMonth`, `PayOsSyncService` (mẫu BackgroundService), `AuthService.ForgotPasswordAsync` (gửi email), `pages/Admin/Packages.tsx`.

## Bẫy cần tránh

- Gán `InvoiceItemId` cho usage phải cùng transaction với tạo hóa đơn, nếu không hai kế toán tạo cùng lúc sẽ tính một lần sử dụng hai lần.
- Hóa đơn đã phát hành không được thêm usage mới; usage đến sau kỳ sẽ vào hóa đơn kỳ sau (theo `UsedOn`).
