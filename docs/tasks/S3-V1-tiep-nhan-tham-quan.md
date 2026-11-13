# S3-V1 · Tiếp nhận & đặt lịch tham quan (UC-17, UC-18, UC-21, UC-22)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Viên | Must | 5 | 3.1 | — | `feature/s3-v1-admissions` |

## Mục tiêu

Khách (chưa đăng nhập) gửi yêu cầu tư vấn và đặt lịch tham quan trên trang công khai. Lễ tân quản lý "phễu" khách: Mới → Đã liên hệ → Đã tham quan → Nhập viện / Không tiếp tục, xác nhận lịch tham quan và gửi email. Task này là nền cho chatbot AI-01 (S4-V1 gọi lại `CreateInquiryAsync`, `BookTourAsync`).

## Tiêu chí hoàn thành

- [ ] Trang công khai `/dat-lich-tham-quan`: chọn ngày (từ mai đến +60 ngày), buổi Sáng/Chiều, số khách, thấy số chỗ còn lại; gửi thành công không cần đăng nhập.
- [ ] Trang chủ có form "Nhận tư vấn" (họ tên, SĐT, người cao tuổi, nhu cầu, gói quan tâm).
- [ ] Gửi quá 5 lần/phút/IP → 429. Buổi đầy (`Facility:TourSlotCapacity`) → 400 với thông báo rõ.
- [ ] Lễ tân (`receptionist`) thấy danh sách yêu cầu (OData, lọc theo trạng thái, tìm theo tên/SĐT), đổi trạng thái, ghi chú, giao cho nhân viên phụ trách.
- [ ] "Nhập viện" từ một yêu cầu: mở form tạo cư dân (đã điền sẵn họ tên), lưu xong yêu cầu chuyển `Admitted` và lưu `ResidentId`.
- [ ] Xác nhận lịch tham quan → gửi email cho khách nếu có email và SMTP đã cấu hình (không có thì vẫn xác nhận, chỉ bỏ qua email).
- [ ] Yêu cầu/lịch mới → thông báo cho mọi Receptionist (chuông).
- [ ] Caregiver/Family gọi API quản lý → 403.

## 1. Database — `01_Schema.sql` mục 9

```sql
IF OBJECT_ID(N'dbo.Inquiries', N'U') IS NULL
CREATE TABLE dbo.Inquiries (
    Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Inquiries PRIMARY KEY,
    FullName            NVARCHAR(150)  NOT NULL,
    Phone               NVARCHAR(30)   NOT NULL,
    Email               NVARCHAR(256)  NULL,
    ElderName           NVARCHAR(150)  NULL,
    ElderAge            INT            NULL,
    CareNeeds           NVARCHAR(1000) NULL,
    InterestedPackageId INT            NULL CONSTRAINT FK_Inquiries_Packages REFERENCES dbo.ServicePackages (Id),
    Source              NVARCHAR(20)   NOT NULL CONSTRAINT DF_Inquiries_Source DEFAULT (N'Web'),
    Status              NVARCHAR(20)   NOT NULL CONSTRAINT DF_Inquiries_Status DEFAULT (N'New'),
    AssignedToId        INT            NULL CONSTRAINT FK_Inquiries_Accounts REFERENCES dbo.Accounts (Id),
    Notes               NVARCHAR(2000) NULL,
    ResidentId          INT            NULL CONSTRAINT FK_Inquiries_Residents REFERENCES dbo.Residents (Id),
    CreatedAt           DATETIME2      NOT NULL CONSTRAINT DF_Inquiries_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt           DATETIME2      NULL,
    CONSTRAINT CK_Inquiries_Source CHECK (Source IN (N'Web', N'Phone', N'WalkIn', N'Chatbot')),
    CONSTRAINT CK_Inquiries_Status CHECK (Status IN (N'New', N'Contacted', N'Visited', N'Admitted', N'Lost')),
    CONSTRAINT CK_Inquiries_Age CHECK (ElderAge IS NULL OR ElderAge BETWEEN 40 AND 120)
);
GO
IF OBJECT_ID(N'dbo.TourBookings', N'U') IS NULL
CREATE TABLE dbo.TourBookings (
    Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TourBookings PRIMARY KEY,
    InquiryId     INT           NOT NULL CONSTRAINT FK_TourBookings_Inquiries REFERENCES dbo.Inquiries (Id),
    VisitDate     DATE          NOT NULL,
    TimeSlot      NVARCHAR(20)  NOT NULL,
    GuestCount    INT           NOT NULL,
    Note          NVARCHAR(500) NULL,
    Status        NVARCHAR(20)  NOT NULL CONSTRAINT DF_TourBookings_Status DEFAULT (N'Requested'),
    ConfirmedById INT           NULL CONSTRAINT FK_TourBookings_Accounts REFERENCES dbo.Accounts (Id),
    CreatedAt     DATETIME2     NOT NULL CONSTRAINT DF_TourBookings_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt     DATETIME2     NULL,
    CONSTRAINT CK_TourBookings_Slot CHECK (TimeSlot IN (N'Morning', N'Afternoon')),
    CONSTRAINT CK_TourBookings_Guests CHECK (GuestCount BETWEEN 1 AND 10),
    CONSTRAINT CK_TourBookings_Status CHECK (Status IN (N'Requested', N'Confirmed', N'Done', N'Cancelled', N'NoShow'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TourBookings_Date_Slot')
    CREATE INDEX IX_TourBookings_Date_Slot ON dbo.TourBookings (VisitDate, TimeSlot, Status);
GO
```

Option mới (PR lõi nhỏ, báo Huy): `FacilityOptions.TourSlotCapacity = 5` + `"TourSlotCapacity": 5` trong `appsettings.json` mục `Facility`.

## 2. Backend

| Layer | File | Nội dung |
| --- | --- | --- |
| BusinessObjects | `Entities/OperationsAdmissions.cs` | `Inquiry`, `TourBooking`, enum `InquiryStatus`, `InquirySource`, `TourSlot`, `TourStatus` (đặt trong file này, không sửa `Enums.cs`) |
| | `NursingHomeContext.Operations.cs` | `DbSet` + mapping, enum `HasConversion<string>()` |
| DAO | `DataAccessObjects/AdmissionDAO.cs` | `ListInquiriesAsync()`, `GetInquiryAsync(id)`, `AddInquiryAsync`, `UpdateInquiryAsync`, `ListToursAsync(from, to)`, **`BookTourAsync(TourBooking, capacity)`** trong transaction Serializable: đếm tổng khách các booking `Requested/Confirmed` cùng ngày + buổi, vượt sức chứa thì ném `BusinessRuleException`; `CountGuestsAsync(date, slot)` cho màn chọn chỗ |
| Repository | `Repositories/.../AdmissionRepository.cs` | Ủy quyền DAO |
| DTOs | `DTOs/Admissions/AdmissionDtos.cs` | `CreateInquiryRequest`, `BookTourRequest` (họ tên, SĐT, email, ngày, buổi, số khách, ghi chú), `InquiryDto` (entity set OData), `TourBookingDto`, `TourSlotAvailabilityDto {date, slot, remaining}`, `UpdateInquiryRequest {status, assignedToId, notes}`, `AdmitInquiryRequest {residentId}` |
| Service | `Services/Implementations/AdmissionService.cs` + interface trong `IOperationsServices.cs` | Xem quy tắc bên dưới |
| Controller | `API/Controllers/AdmissionsController.cs` | Public + quản lý |
| OData | `EdmModelBuilder.Operations.cs` | `builder.EntitySet<InquiryDto>("Inquiries")` + `InquiriesODataController` (policy FrontDesk) |
| Rate limit | `Program.cs` (lõi, 3 dòng) | Thêm policy `PublicForms` 5 lần/phút/IP như `Credentials` |

Quy tắc trong `AdmissionService`:

- `CreateInquiryAsync(request, source)` — public, dùng lại cho chatbot. Chuẩn hóa SĐT (bỏ khoảng trắng), `Status = New`. Báo `NotifyRolesAsync([Receptionist], NotificationType.System, "Yêu cầu tư vấn mới", "<tên> — <SĐT>", "/staff/admissions")`.
- `BookTourAsync(request)` — ngày trong [mai, +60 ngày], không nhận Chủ Nhật (tùy chọn); tạo `Inquiry` (Source Web) rồi `TourBooking` (gọi DAO có kiểm sức chứa). Báo Receptionist.
- `GetAvailabilityAsync(from, to)` — trả số chỗ còn lại mỗi ngày × buổi (`capacity - tổng khách`).
- `UpdateInquiryAsync` — chuyển trạng thái hợp lệ: `New → Contacted → Visited → Admitted|Lost`, cho phép nhảy thẳng `New → Lost`; `Admitted/Lost` là trạng thái cuối. `AssignedToId` phải là Receptionist/Admin đang hoạt động.
- `AdmitAsync(id, residentId)` — cư dân phải tồn tại; đặt `Status = Admitted`, `ResidentId`.
- `ConfirmTourAsync(id, user)` — `Requested → Confirmed`, lưu `ConfirmedById`; nếu có email và `IEmailService.IsConfigured` thì gửi email (ngày, buổi, địa chỉ, hotline từ `FacilityOptions`); lỗi gửi email chỉ log, không làm hỏng xác nhận. `CancelTour`, `MarkDone`, `MarkNoShow` tương tự.

## 3. API

| Method | Đường dẫn | Policy | Ghi chú |
| --- | --- | --- | --- |
| POST | `/api/public/inquiries` | ẩn danh, `PublicForms` | Tạo yêu cầu tư vấn |
| GET | `/api/public/tour-slots?from=&to=` | ẩn danh | Chỗ còn lại |
| POST | `/api/public/tour-bookings` | ẩn danh, `PublicForms` | Đặt lịch tham quan |
| GET | `/odata/Inquiries` | FrontDesk | `$filter=status eq 'New'`, `contains(tolower(fullName),'an')` |
| PUT | `/api/inquiries/{id}` | FrontDesk | Đổi trạng thái / giao / ghi chú |
| POST | `/api/inquiries/{id}/admit` | FrontDesk | `{ residentId }` |
| GET | `/api/tour-bookings?from=&to=` | FrontDesk | Lịch tham quan |
| POST | `/api/tour-bookings/{id}/confirm` · `/cancel` · `/done` · `/no-show` | FrontDesk | |

## 4. Frontend

- `src/types/operations.ts`: `Inquiry`, `TourBooking`, `TourSlotAvailability`.
- `src/api/admissions.ts`: hàm cho các endpoint trên (`odataList<Inquiry>('Inquiries', …)` cho danh sách).
- `src/pages/Operations/TourBooking.tsx` (công khai): `DatePicker` (khóa ngày ngoài [mai, +60]), `Radio` Sáng/Chiều kèm "còn N chỗ", `InputNumber` số khách, form liên hệ. Thành công → `Result` "Đã nhận lịch, lễ tân sẽ gọi xác nhận".
- Form "Nhận tư vấn" nhúng vào `pages/Public/Home.tsx` (file lõi của Viên) — component `InquiryForm` đặt ở `pages/Operations/`.
- `src/pages/Operations/Admissions.tsx` (FrontDesk): `Tabs` gồm **Yêu cầu tư vấn** (bảng OData + `Segmented` theo trạng thái + `Select` đổi trạng thái + nút "Nhập viện" mở `ResidentFormModal` có sẵn ở `pages/Staff/resident/`) và **Lịch tham quan** (chọn tuần, bảng theo ngày/buổi, nút Xác nhận/Hủy/Đã đến/Không đến).
- `src/modules/operations.tsx`: route công khai `/dat-lich-tham-quan`, route `/staff/admissions` (FRONT_DESK), menu "Tiếp nhận & tham quan". Thêm nút "Đặt lịch tham quan" ở `PublicLayout` header.
- `src/utils/status.operations.ts`: `inquiry`, `tour`.

## 5. Dữ liệu demo — `05_DemoSeed.sql` mục 10

3 yêu cầu (New, Contacted, Visited) + 2 lịch tham quan (1 `Requested` ngày mai buổi sáng, 1 `Confirmed` ngày kia buổi chiều). Ngày dùng `DATEADD(DAY, 1, @Today)`.

## 6. Kiểm thử

Thủ công:
1. Ẩn danh: đặt lịch ngày mai buổi sáng 3 khách → thành công; đặt tiếp 3 khách cùng buổi (capacity 5) → báo đầy.
2. `receptionist`: chuông có thông báo; xác nhận lịch; chuyển yêu cầu sang Đã liên hệ → Đã tham quan → Nhập viện (tạo cư dân) → thấy cư dân mới ở trang Cư dân.
3. `caregiver` gọi `GET /odata/Inquiries` bằng Swagger → 403.

Thêm vào `scripts/smoke-test.mjs`:
```js
const inq = await call('POST', '/api/public/inquiries', null, { fullName: 'Khách test', phone: '0901234567', careNeeds: 'Tư vấn gói cơ bản' });
check('Khách tạo yêu cầu tư vấn không cần đăng nhập', inq.status === 200);
const recep = await login('receptionist@nursinghome.local');
const list = await call('GET', "/odata/Inquiries?$filter=status eq 'New'&$count=true", recep.accessToken);
check('Lễ tân thấy yêu cầu mới', list.data['@odata.count'] >= 1);
check('Caregiver không xem được yêu cầu', (await call('GET', '/odata/Inquiries', caregiver.accessToken)).status === 403);
```

## Tham khảo code có sẵn

- Endpoint ẩn danh + rate limit: `AuthController.Login` (`[AllowAnonymous]`, `[EnableRateLimiting]`), cấu hình ở `Program.cs` → `AddRateLimiter`.
- Kiểm sức chứa trong transaction: `RoomDAO.AssignAsync`, `BillingDAO.AddSubscriptionAsync`.
- Gửi email: `AuthService.ForgotPasswordAsync`. OData list: `ODataControllers.cs` → `ResidentsODataController`.
- Form công khai + `Result`: `pages/Public/ForgotPassword.tsx`.

## Bẫy cần tránh

- Endpoint public **không** trả thông tin cư dân hay số điện thoại người khác; chỉ trả số chỗ còn lại.
- Hai người đặt cùng lúc vào chỗ cuối → kiểm sức chứa phải nằm trong transaction của DAO, không kiểm ở Service rồi mới ghi.
- `VisitDate` là `DATE` theo giờ VN: so với `clock.Today`, không so với `DateTime.UtcNow.Date`.
