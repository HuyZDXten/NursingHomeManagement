# S3-H5 · Đánh giá định kỳ, phiếu khám bác sĩ, ngưỡng sinh hiệu riêng (UC-64, UC-67, UC-68)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Huy | Should | 5 | 3.2 | — | `feature/s3-h5-assessments` |

## Mục tiêu

Điều dưỡng làm **đánh giá sàng lọc** định kỳ (nguy cơ té ngã Morse, mức độc lập Barthel ADL, dinh dưỡng MNA rút gọn) — hệ thống tự tính điểm và mức nguy cơ, nhắc khi đến hạn đánh giá lại. Bác sĩ ghi **phiếu khám** theo SOAP. Bác sĩ đặt **ngưỡng cảnh báo sinh hiệu riêng** cho từng cư dân, thay ngưỡng mặc định của cơ sở khi ghi sinh hiệu.

## Tiêu chí hoàn thành

- [ ] Form đánh giá theo từng thang (câu hỏi + lựa chọn có điểm); **điểm tính ở server**, FE chỉ gửi câu trả lời. Lưu mức nguy cơ và ngày đánh giá lại (+90 ngày).
- [ ] Danh sách "Đánh giá đến hạn" (cư dân Active chưa có đánh giá loại X hoặc đã quá `NextDueOn`).
- [ ] Doctor ghi phiếu khám: S (triệu chứng), O (khám), A (nhận định), P (kế hoạch), ngày tái khám, chia sẻ gia đình; chỉ người viết sửa trong 24 giờ.
- [ ] Doctor đặt ngưỡng riêng cho cư dân; `HealthService.RecordAsync` dùng ngưỡng riêng nếu có, nếu không dùng `Facility:VitalAlerts`.
- [ ] Mọi màn hình ghi rõ: **"Công cụ sàng lọc, không thay thế chẩn đoán."**
- [ ] Caregiver chỉ xem (cư dân được giao); Receptionist/Accountant → 403.

## 1. Database — `01_Schema.sql` mục 10

```sql
IF OBJECT_ID(N'dbo.VitalThresholds', N'U') IS NULL
CREATE TABLE dbo.VitalThresholds (
    ResidentId      INT           NOT NULL CONSTRAINT PK_VitalThresholds PRIMARY KEY
                                  CONSTRAINT FK_VitalThresholds_Residents REFERENCES dbo.Residents (Id),
    SystolicHigh    INT           NOT NULL,
    SystolicLow     INT           NOT NULL,
    HeartRateHigh   INT           NOT NULL,
    HeartRateLow    INT           NOT NULL,
    TemperatureHigh DECIMAL(4,1)  NOT NULL,
    SpO2Low         DECIMAL(5,2)  NOT NULL,
    Note            NVARCHAR(500) NULL,
    SetById         INT           NOT NULL CONSTRAINT FK_VitalThresholds_Accounts REFERENCES dbo.Accounts (Id),
    UpdatedAt       DATETIME2     NOT NULL CONSTRAINT DF_VitalThresholds_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_VitalThresholds CHECK (SystolicHigh > SystolicLow AND HeartRateHigh > HeartRateLow)
);
GO
IF OBJECT_ID(N'dbo.Assessments', N'U') IS NULL
CREATE TABLE dbo.Assessments (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Assessments PRIMARY KEY,
    ResidentId   INT           NOT NULL CONSTRAINT FK_Assessments_Residents REFERENCES dbo.Residents (Id),
    Type         NVARCHAR(20)  NOT NULL,
    Score        INT           NOT NULL,
    MaxScore     INT           NOT NULL,
    RiskLevel    NVARCHAR(10)  NOT NULL,
    AnswersJson  NVARCHAR(MAX) NOT NULL,
    Note         NVARCHAR(500) NULL,
    AssessedById INT           NOT NULL CONSTRAINT FK_Assessments_Accounts REFERENCES dbo.Accounts (Id),
    AssessedAt   DATETIME2     NOT NULL CONSTRAINT DF_Assessments_AssessedAt DEFAULT (SYSUTCDATETIME()),
    NextDueOn    DATE          NULL,
    CONSTRAINT CK_Assessments_Type CHECK (Type IN (N'FallRisk', N'Barthel', N'Nutrition')),
    CONSTRAINT CK_Assessments_Risk CHECK (RiskLevel IN (N'Low', N'Medium', N'High'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Assessments_Resident_Type')
    CREATE INDEX IX_Assessments_Resident_Type ON dbo.Assessments (ResidentId, Type, AssessedAt DESC);
GO
IF OBJECT_ID(N'dbo.DoctorVisits', N'U') IS NULL
CREATE TABLE dbo.DoctorVisits (
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DoctorVisits PRIMARY KEY,
    ResidentId      INT            NOT NULL CONSTRAINT FK_DoctorVisits_Residents REFERENCES dbo.Residents (Id),
    DoctorId        INT            NOT NULL CONSTRAINT FK_DoctorVisits_Accounts REFERENCES dbo.Accounts (Id),
    VisitedAt       DATETIME2      NOT NULL,
    Subjective      NVARCHAR(2000) NULL,
    Objective       NVARCHAR(2000) NULL,
    Assessment      NVARCHAR(2000) NOT NULL,
    [Plan]          NVARCHAR(2000) NULL,
    FollowUpOn      DATE           NULL,
    ShareWithFamily BIT            NOT NULL CONSTRAINT DF_DoctorVisits_Share DEFAULT (0),
    CreatedAt       DATETIME2      NOT NULL CONSTRAINT DF_DoctorVisits_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt       DATETIME2      NULL
);
GO
```

## 2. Backend

| Layer | File | Nội dung |
| --- | --- | --- |
| BusinessObjects | `Entities/CareClinical.cs` | `VitalThreshold`, `Assessment`, `DoctorVisit` (cột `Plan` là từ khóa SQL — mapping `HasColumnName("Plan")` vẫn OK vì EF tự bọc `[ ]`) |
| Helper | `Services/Helpers/AssessmentScales.cs` | Định nghĩa câu hỏi + điểm của từng thang (static), `Score(type, answers) → (score, max, risk)`. **Morse**: 6 mục, tổng 0–125, `≥ 45 High`, `25–44 Medium`, `< 25 Low`. **Barthel**: 10 mục, tổng 0–100, `≤ 60 High` (phụ thuộc nhiều), `61–90 Medium`, `> 90 Low`. **Dinh dưỡng (MNA-SF)**: 6 mục, 0–14, `≤ 7 High`, `8–11 Medium`, `≥ 12 Low` |
| DAO | `ClinicalDAO.cs` | CRUD 3 bảng; `GetThresholdAsync(residentId)`; `DueAssessmentsAsync(today)` |
| DTOs | `DTOs/Care/ClinicalDtos.cs` | `AssessmentScaleDto` (câu hỏi cho FE), `SubmitAssessmentRequest {residentId, type, answers: {itemKey: optionKey}}`, `AssessmentDto`, `DoctorVisitDto`, `SaveDoctorVisitRequest`, `ThresholdDto`, `SaveThresholdRequest` |
| Service | `ClinicalService.cs` | Quy tắc bên dưới |
| Service (lõi, Huy sở hữu) | `HealthService.RecordAsync` | `var limits = await clinical.GetThresholdAsync(id) ?? facility.Value.VitalAlerts;` — đổi chữ ký `OutOfRange` nhận `VitalAlertOptions` (map từ ngưỡng riêng). Giữ unit test cũ xanh |
| Controller | `ClinicalController.cs` | |

Quy tắc:
- Server **không tin** điểm từ FE: kiểm mọi mục đã trả lời và lựa chọn hợp lệ, tự tính điểm.
- Đánh giá High (té ngã) → báo Nurse "Nguy cơ té ngã cao: <tên>" (gợi ý: tạo kế hoạch chăm sóc phòng ngã).
- Phiếu khám: chỉ Doctor tạo; sửa khi `DoctorId == user.AccountId` và `CreatedAt` trong 24 giờ; `FollowUpOn` ≥ hôm nay.
- Ngưỡng: chỉ Doctor/Admin; kiểm `High > Low`.

## 3. API

| Method | Đường dẫn | Policy |
| --- | --- | --- |
| GET | `/api/assessments/scales` | ClinicalStaff (câu hỏi các thang) |
| GET | `/api/residents/{id}/assessments` | ClinicalStaff |
| POST | `/api/assessments` | CarePlanners + Doctor |
| GET | `/api/assessments/due` | CarePlanners + Doctor |
| GET · POST · PUT | `/api/residents/{id}/doctor-visits[/{visitId}]` | đọc ClinicalStaff; ghi Doctor |
| GET · PUT | `/api/residents/{id}/vital-thresholds` | đọc ClinicalStaff; ghi Prescribers |

## 4. Frontend

- `ClinicalTabs.tsx`: tab **"Đánh giá"** (lịch sử theo thang + nút "Đánh giá mới" → form sinh từ `/api/assessments/scales`, mỗi mục một `Radio.Group`; kết quả hiện điểm + `Tag` mức nguy cơ) và tab **"Khám bác sĩ"** (danh sách phiếu, modal SOAP).
- Trong `VitalsTab` (file của Huy): khối "Ngưỡng cảnh báo" — hiện ngưỡng đang dùng (riêng/mặc định), Doctor sửa được.
- `pages/Care/DueAssessments.tsx` (`/staff/assessments`, CarePlanners + Doctor): danh sách đến hạn, bấm mở hồ sơ ở tab Đánh giá. Menu "Đánh giá định kỳ".

## 5. Dữ liệu demo — mục 11

NH-0002 có Morse = 45 (High) cách đây 100 ngày (→ đến hạn), NH-0001 có Barthel = 85 (Medium); 1 phiếu khám của `doctor` cho NH-0003 tuần trước; ngưỡng riêng NH-0001 `SystolicHigh = 150`.

## 6. Kiểm thử

1. `nurse` làm đánh giá Morse cho NH-0001 → điểm khớp tính tay; gửi thiếu 1 mục → 400.
2. Ghi sinh hiệu NH-0001 tâm thu 155 → có cảnh báo (ngưỡng riêng 150); cư dân khác 155 → không (mặc định 160).
3. `doctor` sửa phiếu khám của mình sau 24 giờ (sửa `CreatedAt` trong DB để thử) → 400.
4. Unit test: `AssessmentScales.Score("FallRisk", ...)` cho 3 bộ câu trả lời mẫu ra đúng mức.

## Tham khảo code có sẵn

`HealthService.OutOfRange` + test `VitalAlerts_FlagOnlyOutOfRangeValues`, `ClinicalTabs.tsx` → `VitalsTab`, `MedicationTab`.

## Bẫy cần tránh

- Câu hỏi/thang điểm đặt ở **một chỗ** (BE `AssessmentScales`), FE lấy qua API — không chép thang điểm sang FE.
- Đổi chữ ký `OutOfRange` thì sửa luôn test cũ cho xanh.
