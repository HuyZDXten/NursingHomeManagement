# S3-H4 · Kế hoạch chăm sóc tự sinh việc, ADL, bữa ăn, nhắc việc (UC-51, UC-56, UC-57, UC-61)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Huy | Should | 8 (tách 2 PR: kế hoạch + job; ADL + bữa ăn) | 3.2 | (tùy chọn) S3-V2 để lấy người phụ trách | `feature/s3-h4-care-plans` |

## Mục tiêu

Điều dưỡng lập **kế hoạch chăm sóc** lặp lại (ví dụ "Đo đường huyết 06:30 mỗi ngày", "Tắm thứ 2-4-6") và hệ thống **tự sinh công việc** hằng ngày, không phải giao tay. Hộ lý ghi nhanh **ADL** (tắm, vệ sinh, vận động, giấc ngủ, tâm trạng) và **bữa ăn/lượng nước**. Hệ thống nhắc việc sắp đến hạn và báo điều dưỡng khi việc quá hạn.

## Tiêu chí hoàn thành

- [ ] Nurse/Admin tạo kế hoạch cho cư dân: tiêu đề, mục tiêu, từ ngày–đến ngày; nhiều mục, mỗi mục có giờ, ngày trong tuần (T2…CN), hướng dẫn, người làm mặc định.
- [ ] Job chạy mỗi giờ sinh `CareTasks` cho **hôm nay và ngày mai** từ các mục đang hiệu lực; chạy lại không sinh trùng (ràng buộc UNIQUE).
- [ ] Người làm: mặc định của mục → nếu trống thì người phụ trách chính (S3-V2, nếu đã có) → nếu không có thì bỏ qua và báo Nurse "mục chưa có người làm".
- [ ] Nút "Sinh việc ngay" cho một kế hoạch (để demo không phải chờ job).
- [ ] Hộ lý ghi ADL theo hạng mục + mức độ (Tự làm / Cần hỗ trợ / Phụ thuộc hoàn toàn) và bữa ăn (Sáng/Trưa/Tối/Phụ, % ăn được, ml nước); mỗi bữa mỗi ngày một bản ghi (ghi lại thì cập nhật).
- [ ] Nhắc việc: việc Pending có hạn trong 30 phút tới → báo người làm **một lần**; quá hạn 1 giờ chưa xong → báo mọi Nurse **một lần**.
- [ ] Trang ghi nhanh `/staff/daily-logs` cho hộ lý: danh sách cư dân được giao hôm nay, mỗi người một hàng nút bữa ăn + ADL.

## 1. Database — `01_Schema.sql` mục 10

```sql
IF OBJECT_ID(N'dbo.CarePlans', N'U') IS NULL
CREATE TABLE dbo.CarePlans (
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CarePlans PRIMARY KEY,
    ResidentId  INT            NOT NULL CONSTRAINT FK_CarePlans_Residents REFERENCES dbo.Residents (Id),
    Title       NVARCHAR(200)  NOT NULL,
    Goals       NVARCHAR(1000) NULL,
    StartDate   DATE           NOT NULL,
    EndDate     DATE           NULL,
    IsActive    BIT            NOT NULL CONSTRAINT DF_CarePlans_IsActive DEFAULT (1),
    CreatedById INT            NOT NULL CONSTRAINT FK_CarePlans_Accounts REFERENCES dbo.Accounts (Id),
    CreatedAt   DATETIME2      NOT NULL CONSTRAINT DF_CarePlans_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt   DATETIME2      NULL,
    CONSTRAINT CK_CarePlans_Dates CHECK (EndDate IS NULL OR EndDate >= StartDate)
);
GO
IF OBJECT_ID(N'dbo.CarePlanItems', N'U') IS NULL
CREATE TABLE dbo.CarePlanItems (
    Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CarePlanItems PRIMARY KEY,
    CarePlanId        INT            NOT NULL CONSTRAINT FK_CarePlanItems_Plans REFERENCES dbo.CarePlans (Id),
    Title             NVARCHAR(200)  NOT NULL,
    Instructions      NVARCHAR(1000) NULL,
    TimeOfDay         TIME(0)        NOT NULL,
    DaysOfWeek        CHAR(7)        NOT NULL CONSTRAINT DF_CarePlanItems_Days DEFAULT ('1111111'),  -- T2..CN, '1' = có
    DefaultAssigneeId INT            NULL CONSTRAINT FK_CarePlanItems_Accounts REFERENCES dbo.Accounts (Id),
    IsActive          BIT            NOT NULL CONSTRAINT DF_CarePlanItems_IsActive DEFAULT (1),
    CONSTRAINT CK_CarePlanItems_Days CHECK (DaysOfWeek NOT LIKE '%[^01]%')
);
GO
-- Liên kết việc sinh tự động với mục kế hoạch: KHÔNG sửa bảng CareTasks lõi, dùng bảng nối.
IF OBJECT_ID(N'dbo.GeneratedCareTasks', N'U') IS NULL
CREATE TABLE dbo.GeneratedCareTasks (
    CarePlanItemId INT       NOT NULL CONSTRAINT FK_GeneratedCareTasks_Items REFERENCES dbo.CarePlanItems (Id),
    ForDate        DATE      NOT NULL,
    CareTaskId     INT       NOT NULL CONSTRAINT FK_GeneratedCareTasks_Tasks REFERENCES dbo.CareTasks (Id),
    CreatedAt      DATETIME2 NOT NULL CONSTRAINT DF_GeneratedCareTasks_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_GeneratedCareTasks PRIMARY KEY (CarePlanItemId, ForDate)   -- chống sinh trùng
);
GO
IF OBJECT_ID(N'dbo.TaskReminders', N'U') IS NULL
CREATE TABLE dbo.TaskReminders (
    CareTaskId INT          NOT NULL CONSTRAINT FK_TaskReminders_Tasks REFERENCES dbo.CareTasks (Id),
    Kind       NVARCHAR(10) NOT NULL,
    SentAt     DATETIME2    NOT NULL CONSTRAINT DF_TaskReminders_SentAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_TaskReminders PRIMARY KEY (CareTaskId, Kind),
    CONSTRAINT CK_TaskReminders_Kind CHECK (Kind IN (N'DueSoon', N'Overdue'))
);
GO
IF OBJECT_ID(N'dbo.DailyCareLogs', N'U') IS NULL
CREATE TABLE dbo.DailyCareLogs (
    Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DailyCareLogs PRIMARY KEY,
    ResidentId      INT           NOT NULL CONSTRAINT FK_DailyCareLogs_Residents REFERENCES dbo.Residents (Id),
    LogDate         DATE          NOT NULL,
    Category        NVARCHAR(20)  NOT NULL,
    Level           NVARCHAR(20)  NOT NULL,
    Note            NVARCHAR(500) NULL,
    RecordedById    INT           NOT NULL CONSTRAINT FK_DailyCareLogs_Accounts REFERENCES dbo.Accounts (Id),
    RecordedAt      DATETIME2     NOT NULL CONSTRAINT DF_DailyCareLogs_RecordedAt DEFAULT (SYSUTCDATETIME()),
    ShareWithFamily BIT           NOT NULL CONSTRAINT DF_DailyCareLogs_Share DEFAULT (1),
    CONSTRAINT CK_DailyCareLogs_Category CHECK (Category IN (N'Bathing', N'Toileting', N'Mobility', N'Sleep', N'Mood', N'Other')),
    CONSTRAINT CK_DailyCareLogs_Level CHECK (Level IN (N'Independent', N'NeedsHelp', N'Dependent', N'Good', N'Fair', N'Poor'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DailyCareLogs_Resident_Date')
    CREATE INDEX IX_DailyCareLogs_Resident_Date ON dbo.DailyCareLogs (ResidentId, LogDate);
GO
IF OBJECT_ID(N'dbo.MealLogs', N'U') IS NULL
CREATE TABLE dbo.MealLogs (
    Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MealLogs PRIMARY KEY,
    ResidentId      INT           NOT NULL CONSTRAINT FK_MealLogs_Residents REFERENCES dbo.Residents (Id),
    MealDate        DATE          NOT NULL,
    Meal            NVARCHAR(10)  NOT NULL,
    IntakePercent   INT           NOT NULL,
    FluidMl         INT           NULL,
    Note            NVARCHAR(500) NULL,
    RecordedById    INT           NOT NULL CONSTRAINT FK_MealLogs_Accounts REFERENCES dbo.Accounts (Id),
    RecordedAt      DATETIME2     NOT NULL CONSTRAINT DF_MealLogs_RecordedAt DEFAULT (SYSUTCDATETIME()),
    ShareWithFamily BIT           NOT NULL CONSTRAINT DF_MealLogs_Share DEFAULT (1),
    CONSTRAINT UQ_MealLogs UNIQUE (ResidentId, MealDate, Meal),
    CONSTRAINT CK_MealLogs_Meal CHECK (Meal IN (N'Breakfast', N'Lunch', N'Dinner', N'Snack')),
    CONSTRAINT CK_MealLogs_Intake CHECK (IntakePercent BETWEEN 0 AND 100),
    CONSTRAINT CK_MealLogs_Fluid CHECK (FluidMl IS NULL OR FluidMl BETWEEN 0 AND 5000)
);
GO
```

## 2. Backend

| Layer | File | Nội dung |
| --- | --- | --- |
| BusinessObjects | `Entities/CareDaily.cs` | `CarePlan` (+ `Items`), `CarePlanItem`, `GeneratedCareTask`, `TaskReminder`, `DailyCareLog`, `MealLog` |
| DAO | `CarePlanDAO.cs` | CRUD kế hoạch/mục; `ActiveItemsForDateAsync(date)` (kế hoạch IsActive, ngày trong khoảng, mục IsActive, cư dân Active); **`GenerateTaskAsync(item, forDate, dueAtUtc, assigneeId, createdById)`**: một transaction — thêm `CareTask`, `SaveChanges`, thêm `GeneratedCareTask`; vi phạm PK (đã sinh) → rollback, trả `false` |
| DAO | `DailyLogDAO.cs` | `AddLogAsync`, `ListLogsAsync(residentId, date)`, `UpsertMealAsync` (MERGE theo UNIQUE), `ListMealsAsync` |
| DAO | `ReminderDAO.cs` (Care) | `DueSoonWithoutReminderAsync(nowUtc, nowUtc+30')`, `OverdueWithoutReminderAsync(nowUtc-60')`, `MarkSentAsync(taskId, kind)` |
| Service | `CarePlanService.cs` | `GenerateForDatesAsync(dates)` dùng chung cho job + nút "Sinh việc ngay"; tạo việc với `CreatedById = plan.CreatedById` |
| Service | `DailyLogService.cs`, `TaskReminderService.cs` | |
| Job | `API/BackgroundJobs/CarePlanJob.cs` (mỗi giờ: sinh việc hôm nay + mai) và `TaskReminderJob.cs` (mỗi 5 phút) | mẫu: `PayOsSyncService` |
| Controller | `CarePlansController.cs`, `DailyLogsController.cs` | |

Quy tắc:
- `DaysOfWeek[i]` với i = 0 là Thứ Hai: `int i = ((int)date.DayOfWeek + 6) % 7`.
- `DueAt = clock.ToUtc(forDate, item.TimeOfDay)`; tiêu đề việc = `item.Title`, hướng dẫn = `item.Instructions`.
- Người làm mặc định phải còn hoạt động và có role Nurse/Caregiver; người phụ trách chính lấy qua `StaffingDAO.IsPrimaryCaregiverAsync`/danh sách của Viên — nếu S3-V2 chưa merge thì bỏ qua bước này.
- Ghi ADL/bữa ăn: `access.RequireAsync(user, residentId, ResidentSection.Care)`; ngày không ở tương lai, tối đa lùi 2 ngày (trừ Nurse/Admin).

## 3. API

| Method | Đường dẫn | Policy |
| --- | --- | --- |
| GET | `/api/residents/{id}/care-plans` | ClinicalStaff |
| POST · PUT | `/api/care-plans[/{id}]` (kèm danh sách mục) | CarePlanners |
| POST | `/api/care-plans/{id}/deactivate` | CarePlanners |
| POST | `/api/care-plans/generate?days=2` | CarePlanners (sinh ngay cho mọi kế hoạch) |
| GET · POST | `/api/residents/{id}/daily-logs?date=` | ClinicalStaff |
| GET | `/api/residents/{id}/meals?date=` | ClinicalStaff |
| PUT | `/api/residents/{id}/meals` `{mealDate, meal, intakePercent, fluidMl, note}` | ClinicalStaff (tạo hoặc cập nhật) |

## 4. Frontend

- `ClinicalTabs.tsx`: tab **"Kế hoạch chăm sóc"** (danh sách kế hoạch, modal tạo/sửa với `Form.List` các mục: tiêu đề, giờ `TimePicker`, `Checkbox.Group` T2…CN, người làm `AccountSelect roles={['Nurse','Caregiver']}`) và tab **"ADL & bữa ăn"** (chọn ngày, bảng ADL + 4 ô bữa ăn).
- `pages/Care/DailyLogs.tsx` (`/staff/daily-logs`, CLINICAL_STAFF): ghi nhanh cho cư dân được giao hôm nay — mỗi hàng: tên + 4 nút bữa ăn (bấm mở popover chọn 0/25/50/75/100% + ml nước) + nút ADL.
- Menu "Ghi nhận hằng ngày".

## 5. Dữ liệu demo — mục 11

1 kế hoạch cho NH-0004 ("Theo dõi đường huyết": 06:30 hằng ngày, người làm caregiver2; "Tắm" 09:00 T2-T4-T6) và vài `MealLogs`/`DailyCareLogs` hôm qua cho NH-0001, NH-0002 (ShareWithFamily = 1). **Không** seed `GeneratedCareTasks`: để job/nút tự sinh khi demo.

## 6. Kiểm thử

1. `nurse` tạo kế hoạch → bấm "Sinh việc ngay" → trang Công việc chăm sóc của `caregiver2` có việc mới; bấm lần 2 → không trùng.
2. Tạo việc hạn 20 phút nữa → trong ≤ 5 phút người làm nhận chuông "sắp đến hạn" đúng 1 lần.
3. `caregiver` ghi bữa trưa NH-0001 50% rồi sửa 75% → chỉ 1 bản ghi.
4. `caregiver` ghi ADL cho NH-0003 (không được giao) → 403.
5. Unit test: hàm chọn ngày trong tuần, `DaysOfWeek = '1010100'` chỉ sinh T2, T4, T6.

## Tham khảo code có sẵn

`CareTaskService.CreateAsync` (tạo việc + thông báo), `PayOsSyncService` (job định kỳ có scope), `CareDAO`, `pages/Staff/CareTasks.tsx`.

## Bẫy cần tránh

- Job chạy trong `BackgroundService` phải tạo scope (`IServiceScopeFactory`) để lấy service scoped.
- Tạo việc + bảng nối phải cùng transaction; nếu tạo việc xong mà ghi bảng nối lỗi sẽ có việc mồ côi bị sinh lại.
- Không thêm cột vào `CareTasks` (bảng lõi) — dùng bảng `GeneratedCareTasks`, `TaskReminders`.
