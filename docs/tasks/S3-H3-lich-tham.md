# S3-H3 · Đặt & duyệt lịch thăm (UC-103, UC-104)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Huy | Must | 5 | 3.1 | — | `feature/s3-h3-visits` |

## Mục tiêu

Gia đình đặt lịch thăm người thân theo khung giờ (1 tiếng/khung, trong giờ thăm của cơ sở), có giới hạn số khách mỗi khung. Lễ tân duyệt/từ chối, check-in khi khách đến và đánh dấu hoàn tất; gia đình nhận thông báo và email xác nhận.

## Tiêu chí hoàn thành

- [ ] Khung giờ sinh từ `Facility:VisitingHours` (đã có, vd. `"08:00-11:00, 14:00-20:00"` → 08–09, 09–10, 10–11, 14–15 …).
- [ ] Family chọn người thân (cư dân đang lưu trú, có liên kết) + ngày (hôm nay → +30 ngày; hôm nay chỉ các khung chưa qua) + khung giờ + số khách (1–5) + tên khách; thấy số chỗ còn lại mỗi khung.
- [ ] Tổng khách các lịch `Requested/Approved` trong một khung ≤ `Facility:VisitSlotCapacity` (mặc định 8); mỗi tài khoản gia đình tối đa 1 lịch đang hiệu lực mỗi ngày.
- [ ] Family hủy lịch của mình trước giờ bắt đầu; thấy danh sách lịch sắp tới và đã qua.
- [ ] Lễ tân xem theo ngày, duyệt/từ chối (lý do), check-in, hoàn tất, đánh dấu không đến; mỗi thay đổi báo gia đình (chuông + email nếu có cấu hình).
- [ ] Family đặt cho cư dân không liên kết → 403/404; Caregiver duyệt → 403.

## 1. Database — `01_Schema.sql` mục 10

```sql
IF OBJECT_ID(N'dbo.VisitBookings', N'U') IS NULL
CREATE TABLE dbo.VisitBookings (
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_VisitBookings PRIMARY KEY,
    ResidentId      INT           NOT NULL CONSTRAINT FK_VisitBookings_Residents REFERENCES dbo.Residents (Id),
    FamilyAccountId INT           NOT NULL CONSTRAINT FK_VisitBookings_Accounts REFERENCES dbo.Accounts (Id),
    VisitDate       DATE          NOT NULL,
    SlotStart       TIME(0)       NOT NULL,
    SlotEnd         TIME(0)       NOT NULL,
    GuestCount      INT           NOT NULL,
    GuestNames      NVARCHAR(500) NULL,
    Note            NVARCHAR(500) NULL,
    Status          NVARCHAR(20)  NOT NULL CONSTRAINT DF_VisitBookings_Status DEFAULT (N'Requested'),
    ReviewedById    INT           NULL CONSTRAINT FK_VisitBookings_Reviewer REFERENCES dbo.Accounts (Id),
    ReviewNote      NVARCHAR(500) NULL,
    CheckedInAt     DATETIME2     NULL,
    CreatedAt       DATETIME2     NOT NULL CONSTRAINT DF_VisitBookings_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt       DATETIME2     NULL,
    CONSTRAINT CK_VisitBookings_Guests CHECK (GuestCount BETWEEN 1 AND 5),
    CONSTRAINT CK_VisitBookings_Slot CHECK (SlotEnd > SlotStart),
    CONSTRAINT CK_VisitBookings_Status CHECK (Status IN (N'Requested', N'Approved', N'Rejected', N'CheckedIn', N'Completed', N'Cancelled', N'NoShow'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VisitBookings_Date_Slot')
    CREATE INDEX IX_VisitBookings_Date_Slot ON dbo.VisitBookings (VisitDate, SlotStart, Status);
GO
```

Option mới (PR lõi nhỏ, báo Viên): `FacilityOptions.VisitSlotCapacity = 8` + `"VisitSlotCapacity": 8` trong `appsettings.json` mục `Facility`.

## 2. Backend

| Layer | File | Nội dung |
| --- | --- | --- |
| BusinessObjects | `Entities/CareVisits.cs` | `VisitBooking` + enum `VisitStatus` |
| Helper | `Services/Helpers/VisitingHours.cs` | `IReadOnlyList<(TimeOnly Start, TimeOnly End)> Slots(string config)` — tách theo `,`, mỗi đoạn `HH:mm-HH:mm` chia 1 tiếng; có unit test |
| DAO | `VisitDAO.cs` | `CountGuestsAsync(date)` (theo từng khung), **`BookAsync(VisitBooking, capacity)`** transaction Serializable: kiểm tổng khách khung + lịch hiệu lực cùng ngày của tài khoản; `ListForFamilyAsync`, `ListByDateAsync(date, status?)`, `UpdateStatusAsync(id, expectedStatuses[], apply)` |
| DTOs | `DTOs/Care/VisitDtos.cs` | `VisitSlotDto {start, end, remaining, isPast}`, `BookVisitRequest`, `VisitBookingDto`, `ReviewVisitRequest {note}` |
| Service | `VisitService.cs` | Quy tắc bên dưới |
| Controller | `VisitsController.cs` | |

Quy tắc:
- Family đặt: link `ResidentFamilyLinks` (bất kỳ cờ nào), cư dân `Active`, ngày trong [hôm nay, +30], khung phải thuộc danh sách `Slots(...)` và chưa bắt đầu (so giờ VN qua `FacilityClock`).
- Chuyển trạng thái: `Requested → Approved|Rejected|Cancelled`; `Approved → CheckedIn|Cancelled|NoShow`; `CheckedIn → Completed`. Sai luồng → `BusinessRuleException`.
- Hủy bởi gia đình: chỉ lịch của mình, trước `SlotStart`.
- Mỗi lần đổi trạng thái → `NotifyAsync([familyId], NotificationType.System, "Lịch thăm <ngày> <giờ>: <trạng thái>", ..., "/family/visits")`; duyệt thì gửi email nếu `IEmailService.IsConfigured` và tài khoản có email.
- Đặt mới → báo mọi Receptionist.

## 3. API

| Method | Đường dẫn | Policy |
| --- | --- | --- |
| GET | `/api/visits/slots?date=` | FamilyOnly + FrontDesk (policy mới `VisitViewers` hoặc `[Authorize]` + kiểm role) |
| POST | `/api/visits` | FamilyOnly |
| GET | `/api/visits/mine` | FamilyOnly |
| POST | `/api/visits/{id}/cancel` | FamilyOnly |
| GET | `/api/visits?date=&status=` | FrontDesk |
| POST | `/api/visits/{id}/approve` · `/reject` · `/check-in` · `/complete` · `/no-show` | FrontDesk |

## 4. Frontend

- `types/care.ts`, `api/visits.ts`, `utils/status.care.ts` (`visit`).
- `pages/Family/FamilyVisits.tsx`: form đặt lịch (`Select` người thân từ `portalApi.familyResidents()`, `DatePicker`, lưới nút khung giờ hiện "còn N chỗ", disabled khi hết/đã qua, số khách, tên khách) + bảng lịch sắp tới / đã qua với nút Hủy.
- `pages/Care/Visits.tsx` (Lễ tân): chọn ngày, bảng theo khung giờ, nút Duyệt/Từ chối/Check-in/Hoàn tất/Không đến; `Badge` số lịch chờ duyệt.
- `src/modules/care.tsx`: route `/family/visits` (Family), `/staff/visits` (FRONT_DESK); menu "Lịch thăm" cho cả hai.

## 5. Dữ liệu demo — mục 11

`family` có 1 lịch `Approved` ngày mai 09:00–10:00 (2 khách) và 1 lịch `Completed` tuần trước; `family2` có 1 lịch `Requested` ngày mai 15:00–16:00.

## 6. Kiểm thử

1. `family` đặt ngày mai 09:00 thêm 1 lịch → báo "đã có lịch trong ngày".
2. Giảm `VisitSlotCapacity` còn 3 (Development), `family2` đặt 2 khách khung 09:00 (đã có 2) → báo đầy.
3. `receptionist` duyệt lịch của `family2` → `family2` thấy chuông; check-in rồi hoàn tất.
4. `family2` gọi `POST /api/visits` cho NH-0001 → 403/404.
5. Unit test `VisitingHours.Slots("08:00-11:00, 14:00-16:00")` → 5 khung.

## Tham khảo code có sẵn

`RoomDAO.AssignAsync` (kiểm sức chứa trong transaction), `FamilyService` (kiểm liên kết), `FacilityOptions.VisitingHours`, `pages/Family/FamilyHome.tsx`.

## Bẫy cần tránh

- "Khung đã qua" tính theo giờ VN (`FacilityClock`), không theo giờ máy trình duyệt.
- Kiểm sức chứa + giới hạn 1 lịch/ngày phải nằm trong transaction DAO, không kiểm ở Service rồi mới ghi.
