# S3-V2 · Ca trực & phân công phụ trách (UC-44, UC-45, UC-46)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Viên | Must | 5 | 3.1 | — | `feature/s3-v2-shifts` |

## Mục tiêu

Admin lập lịch ca theo tuần (Sáng/Chiều/Đêm) cho Doctor, Nurse, Caregiver và phân công nhân viên phụ trách chính từng cư dân. Mỗi nhân viên xem được lịch ca của mình. Dữ liệu này dùng tiếp ở S3-V4 (bàn giao ca), S3-H4 (người làm mặc định khi tự sinh việc) và S4-V2 (AI tóm tắt ca).

## Tiêu chí hoàn thành

- [ ] Bảng tuần: hàng = nhân viên lâm sàng, cột = 7 ngày, ô = các ca; Admin bấm ô để thêm/xóa ca.
- [ ] Không xếp một người 2 lần cùng ca cùng ngày (UNIQUE); xếp 2 ca liền nhau trong một ngày thì cảnh báo nhưng vẫn cho.
- [ ] Nút "Sao chép tuần trước" (bỏ qua ô đã có).
- [ ] `nurse`/`caregiver` mở "Lịch ca của tôi" thấy 14 ngày tới; không thấy nút sửa.
- [ ] Tab "Phân công phụ trách": chọn cư dân + nhân viên + ca (tùy chọn) + từ ngày; kết thúc phân công; một cư dân chỉ một người phụ trách chính đang hiệu lực mỗi ca.
- [ ] Caregiver được phân công phụ trách cư dân **xem được hồ sơ** cư dân đó kể cả khi chưa có việc (mở rộng `ResidentAccessService`, có test).
- [ ] Receptionist/Accountant/Family gọi API sửa → 403.

## 1. Database — `01_Schema.sql` mục 9

```sql
IF OBJECT_ID(N'dbo.Shifts', N'U') IS NULL
CREATE TABLE dbo.Shifts (
    Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Shifts PRIMARY KEY,
    Code      NVARCHAR(20) NOT NULL CONSTRAINT UQ_Shifts_Code UNIQUE,
    Name      NVARCHAR(50) NOT NULL,
    StartTime TIME(0)      NOT NULL,
    EndTime   TIME(0)      NOT NULL,   -- ca đêm: EndTime < StartTime (qua ngày hôm sau)
    IsActive  BIT          NOT NULL CONSTRAINT DF_Shifts_IsActive DEFAULT (1)
);
GO
IF OBJECT_ID(N'dbo.ShiftAssignments', N'U') IS NULL
CREATE TABLE dbo.ShiftAssignments (
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShiftAssignments PRIMARY KEY,
    AccountId   INT           NOT NULL CONSTRAINT FK_ShiftAssignments_Accounts REFERENCES dbo.Accounts (Id),
    ShiftId     INT           NOT NULL CONSTRAINT FK_ShiftAssignments_Shifts REFERENCES dbo.Shifts (Id),
    WorkDate    DATE          NOT NULL,
    Note        NVARCHAR(500) NULL,
    CreatedById INT           NOT NULL CONSTRAINT FK_ShiftAssignments_CreatedBy REFERENCES dbo.Accounts (Id),
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_ShiftAssignments_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_ShiftAssignments UNIQUE (AccountId, WorkDate, ShiftId)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ShiftAssignments_Date')
    CREATE INDEX IX_ShiftAssignments_Date ON dbo.ShiftAssignments (WorkDate, ShiftId);
GO
IF OBJECT_ID(N'dbo.ResidentCareAssignments', N'U') IS NULL
CREATE TABLE dbo.ResidentCareAssignments (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ResidentCareAssignments PRIMARY KEY,
    ResidentId   INT       NOT NULL CONSTRAINT FK_RCA_Residents REFERENCES dbo.Residents (Id),
    AccountId    INT       NOT NULL CONSTRAINT FK_RCA_Accounts REFERENCES dbo.Accounts (Id),
    ShiftId      INT       NULL CONSTRAINT FK_RCA_Shifts REFERENCES dbo.Shifts (Id),   -- NULL = mọi ca
    StartDate    DATE      NOT NULL,
    EndDate      DATE      NULL,
    AssignedById INT       NOT NULL CONSTRAINT FK_RCA_AssignedBy REFERENCES dbo.Accounts (Id),
    CreatedAt    DATETIME2 NOT NULL CONSTRAINT DF_RCA_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_RCA_Dates CHECK (EndDate IS NULL OR EndDate >= StartDate)
);
GO
-- Một người phụ trách chính đang hiệu lực cho mỗi (cư dân, ca).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_RCA_ActivePrimary')
    CREATE UNIQUE INDEX UX_RCA_ActivePrimary ON dbo.ResidentCareAssignments (ResidentId, ShiftId) WHERE EndDate IS NULL;
GO
```

`04_ReferenceData.sql` (file của Viên): 3 ca cố định — `MORNING` Ca sáng 06:00–14:00, `AFTERNOON` Ca chiều 14:00–22:00, `NIGHT` Ca đêm 22:00–06:00 (Id 1, 2, 3 bằng `IDENTITY_INSERT`).

> Lưu ý filtered unique index với `ShiftId NULL`: SQL Server coi các NULL là bằng nhau trong unique index, nên mỗi cư dân chỉ có một phân công "mọi ca" đang hiệu lực — đúng ý muốn.

## 2. Backend

| Layer | File | Nội dung |
| --- | --- | --- |
| BusinessObjects | `Entities/OperationsStaffing.cs` | `Shift`, `ShiftAssignment` (nav `Account`, `Shift`), `ResidentCareAssignment` (nav `Resident`, `Account`, `Shift`) |
| DAO | `StaffingDAO.cs` | `ListShiftsAsync`, `ListAssignmentsAsync(from, to, accountId?)`, `AddAssignmentAsync`, `DeleteAssignmentAsync`, **`CopyWeekAsync(fromMonday, toMonday, byId)`** (một transaction, bỏ qua bản ghi đã có), `ListCareAssignmentsAsync(residentId?, accountId?, activeOnly)`, `AddCareAssignmentAsync`, `EndCareAssignmentAsync(id, endDate)`, `IsPrimaryCaregiverAsync(residentId, accountId, today)` |
| DTOs | `DTOs/Staffing/StaffingDtos.cs` | `ShiftDto`, `ShiftAssignmentDto {id, accountId, accountName, role, shiftId, shiftCode, workDate, note}`, `CreateShiftAssignmentRequest`, `CopyWeekRequest {fromWeekStart, toWeekStart}`, `CareAssignmentDto`, `CreateCareAssignmentRequest` |
| Service | `StaffingService.cs` (+ `IStaffingService` trong `IOperationsServices.cs`) | Quy tắc bên dưới |
| Controller | `ShiftsController.cs` | |
| **Lõi** | `ResidentAccessService.cs` | Caregiver: `HasOpenTaskAsync(...) || IsPrimaryCaregiverAsync(...)`; `VisibleResidentIdsAsync` hợp 2 tập. PR riêng, báo Huy (ảnh hưởng mọi màn chăm sóc) |

Quy tắc:
- Chỉ xếp ca cho tài khoản đang hoạt động có role `Doctor, Nurse, Caregiver`.
- `WorkDate` không ở quá khứ (Admin sửa quá khứ thì trả `BusinessRuleException`).
- Cùng người, cùng ngày đã có ca liền kề (Sáng + Chiều, Chiều + Đêm) → vẫn tạo, trả về `warning` trong DTO để FE hiện.
- Xếp ca/phân công xong → thông báo cho người được xếp (`NotificationType.System`, link `/staff/shifts`).
- Phân công phụ trách: người được giao phải là Nurse/Caregiver; cư dân Active; trùng (vi phạm `UX_RCA_ActivePrimary`) → middleware trả 409 "đã có người phụ trách".

## 3. API

| Method | Đường dẫn | Policy | Ghi chú |
| --- | --- | --- | --- |
| GET | `/api/shifts` | Staff | Danh mục ca |
| GET | `/api/shift-assignments?from=&to=` | ClinicalStaff | Admin/Nurse thấy tất cả; Doctor/Caregiver chỉ của mình |
| GET | `/api/shift-assignments/mine?days=14` | Staff | UC-46 |
| POST | `/api/shift-assignments` | AdminOnly | |
| DELETE | `/api/shift-assignments/{id}` | AdminOnly | |
| POST | `/api/shift-assignments/copy-week` | AdminOnly | trả số bản ghi đã chép |
| GET | `/api/care-assignments?residentId=&accountId=` | ClinicalStaff | |
| POST | `/api/care-assignments` | CarePlanners | |
| POST | `/api/care-assignments/{id}/end` | CarePlanners | `EndDate = clock.Today` |

## 4. Frontend

- `src/pages/Operations/Shifts.tsx` với `Tabs`:
  - **Lịch ca tuần**: `DatePicker picker="week"`; `Table` cột động 7 ngày; ô hiển thị `Tag` màu theo ca; Admin bấm ô → `Popover` chọn ca thêm/xóa; nút "Sao chép tuần trước".
  - **Lịch ca của tôi**: danh sách 14 ngày (`List`), hiện với mọi role lâm sàng.
  - **Phân công phụ trách** (CarePlanners): bảng phân công đang hiệu lực + modal thêm (`ResidentSelect`, `AccountSelect roles={['Nurse','Caregiver']}`, chọn ca, ngày bắt đầu).
- `src/modules/operations.tsx`: route `/staff/shifts` (CLINICAL_STAFF), menu "Ca trực".
- `src/utils/status.operations.ts`: `shift` (MORNING/AFTERNOON/NIGHT với màu).

## 5. Dữ liệu demo

`04`: 3 ca. `05` mục 10: tuần hiện tại — `nurse` (5) ca sáng T2–T6, `caregiver` (6) ca sáng, `caregiver2` (7) ca chiều; phân công phụ trách: NH-0001 → caregiver (6), NH-0004 → caregiver2 (7). Ngày tính từ `@Today` (`DATEADD(DAY, 1 - DATEPART(WEEKDAY, @Today), @Today)` cho đầu tuần, chú ý `SET DATEFIRST 1`).

## 6. Kiểm thử

1. `admin`: xếp ca cho caregiver2 thứ Hai tuần sau, thử xếp trùng → 409; sao chép tuần trước.
2. `caregiver2`: thấy chuông thông báo, "Lịch ca của tôi" có ca mới; không thấy nút sửa.
3. `nurse`: phân công `caregiver` phụ trách NH-0003 → đăng nhập `caregiver` mở được `/staff/residents/3` (trước đó 403).
4. Unit test (`NursingHome.Tests`): `ResidentAccessService` với repository giả — caregiver có phân công phụ trách thì `CanReadAsync(..., Health)` = true.

Smoke test: thêm check "Caregiver có phân công phụ trách đọc được hồ sơ" và "Receptionist tạo ca → 403".

## Tham khảo code có sẵn

- Bảng cột động + Popover: tham khảo `pages/Staff/Rooms.tsx` (thẻ giường), `Table` của antd.
- Transaction nhiều dòng: `ResidentDAO.DischargeAsync`.
- Lookup tài khoản theo role: `AccountSelect` (`components/Common.tsx`).

## Bẫy cần tránh

- Ca đêm qua ngày: thời điểm bắt đầu = `WorkDate + StartTime`, kết thúc = `WorkDate + 1 ngày + EndTime`. Viết hàm `ShiftWindow(workDate, shift)` dùng chung (S3-V4 cần).
- Đừng sửa `ResidentAccessService` chung PR với tính năng; tách PR để Huy review vì ảnh hưởng mọi màn chăm sóc.
