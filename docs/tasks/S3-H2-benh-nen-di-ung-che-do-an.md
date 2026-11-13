# S3-H2 · Bệnh nền, dị ứng, chế độ ăn + cảnh báo dị ứng khi nhập thuốc (UC-66, UC-71, UC-82)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Huy | Must (UC-66) / Should | 5 | 3.1 | — | `feature/s3-h2-medical-profile` |

## Mục tiêu

Hồ sơ y tế của cư dân có danh sách bệnh nền, dị ứng và chế độ ăn hiện hành. Khi bác sĩ nhập lịch thuốc, hệ thống **cảnh báo theo quy tắc** nếu tên thuốc trùng một dị ứng thuốc đã ghi hoặc trùng thuốc đang dùng; bác sĩ phải xác nhận mới lưu được. Badge "Dị ứng" hiện ở đầu hồ sơ cư dân.

## Tiêu chí hoàn thành

- [ ] Doctor/Nurse thêm/sửa bệnh nền (tên, ngày chẩn đoán, đang điều trị/đã khỏi, ghi chú), dị ứng (tác nhân, loại Thuốc/Thực phẩm/Khác, phản ứng, mức độ). Caregiver chỉ xem (cư dân được giao).
- [ ] Mỗi cư dân tối đa **một chế độ ăn đang hiệu lực**; tạo chế độ mới thì chế độ cũ tự kết thúc hôm qua.
- [ ] Nhập lịch thuốc: nếu tên thuốc chứa tác nhân dị ứng loại **Drug** (không phân biệt hoa thường, bỏ dấu) hoặc trùng tên thuốc đang dùng → FE hiện cảnh báo, chỉ lưu khi bác sĩ tick "Tôi đã xem cảnh báo". Gọi API thiếu xác nhận → 400.
- [ ] Badge đỏ "Dị ứng: …" cạnh tên cư dân ở trang hồ sơ.
- [ ] Gia đình có quyền Health thấy bệnh nền + dị ứng + chế độ ăn (chỉ đọc).

## 1. Database — `01_Schema.sql` mục 10

```sql
IF OBJECT_ID(N'dbo.MedicalConditions', N'U') IS NULL
CREATE TABLE dbo.MedicalConditions (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MedicalConditions PRIMARY KEY,
    ResidentId   INT            NOT NULL CONSTRAINT FK_MedicalConditions_Residents REFERENCES dbo.Residents (Id),
    Name         NVARCHAR(200)  NOT NULL,
    DiagnosedOn  DATE           NULL,
    Status       NVARCHAR(20)   NOT NULL CONSTRAINT DF_MedicalConditions_Status DEFAULT (N'Active'),
    Notes        NVARCHAR(1000) NULL,
    RecordedById INT            NOT NULL CONSTRAINT FK_MedicalConditions_Accounts REFERENCES dbo.Accounts (Id),
    CreatedAt    DATETIME2      NOT NULL CONSTRAINT DF_MedicalConditions_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt    DATETIME2      NULL,
    CONSTRAINT CK_MedicalConditions_Status CHECK (Status IN (N'Active', N'Resolved'))
);
GO
IF OBJECT_ID(N'dbo.Allergies', N'U') IS NULL
CREATE TABLE dbo.Allergies (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Allergies PRIMARY KEY,
    ResidentId   INT           NOT NULL CONSTRAINT FK_Allergies_Residents REFERENCES dbo.Residents (Id),
    Allergen     NVARCHAR(150) NOT NULL,
    Type         NVARCHAR(10)  NOT NULL,
    Reaction     NVARCHAR(500) NULL,
    Severity     NVARCHAR(10)  NOT NULL,
    RecordedById INT           NOT NULL CONSTRAINT FK_Allergies_Accounts REFERENCES dbo.Accounts (Id),
    CreatedAt    DATETIME2     NOT NULL CONSTRAINT DF_Allergies_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Allergies UNIQUE (ResidentId, Allergen),
    CONSTRAINT CK_Allergies_Type CHECK (Type IN (N'Drug', N'Food', N'Other')),
    CONSTRAINT CK_Allergies_Severity CHECK (Severity IN (N'Mild', N'Moderate', N'Severe'))
);
GO
IF OBJECT_ID(N'dbo.DietPlans', N'U') IS NULL
CREATE TABLE dbo.DietPlans (
    Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DietPlans PRIMARY KEY,
    ResidentId     INT            NOT NULL CONSTRAINT FK_DietPlans_Residents REFERENCES dbo.Residents (Id),
    DietType       NVARCHAR(30)   NOT NULL,
    Texture        NVARCHAR(20)   NOT NULL CONSTRAINT DF_DietPlans_Texture DEFAULT (N'Normal'),
    Restrictions   NVARCHAR(1000) NULL,
    StartDate      DATE           NOT NULL,
    EndDate        DATE           NULL,
    PrescribedById INT            NOT NULL CONSTRAINT FK_DietPlans_Accounts REFERENCES dbo.Accounts (Id),
    CreatedAt      DATETIME2      NOT NULL CONSTRAINT DF_DietPlans_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_DietPlans_Type CHECK (DietType IN (N'Normal', N'Diabetic', N'LowSalt', N'LowFat', N'Renal', N'Vegetarian', N'Other')),
    CONSTRAINT CK_DietPlans_Texture CHECK (Texture IN (N'Normal', N'Soft', N'Minced', N'Pureed', N'Liquid')),
    CONSTRAINT CK_DietPlans_Dates CHECK (EndDate IS NULL OR EndDate >= StartDate)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_DietPlans_Active')
    CREATE UNIQUE INDEX UX_DietPlans_Active ON dbo.DietPlans (ResidentId) WHERE EndDate IS NULL;
GO
```

## 2. Backend

| Layer | File | Nội dung |
| --- | --- | --- |
| BusinessObjects | `Entities/CareMedicalProfile.cs` | `MedicalCondition`, `Allergy`, `DietPlan` + enum |
| DAO | `MedicalProfileDAO.cs` | CRUD 3 bảng theo cư dân; `ReplaceDietPlanAsync(newPlan)` — transaction: kết thúc plan đang mở (`EndDate = StartDate mới - 1`) rồi thêm plan mới; `ListDrugAllergensAsync(residentId)` |
| DTOs | `DTOs/Care/MedicalProfileDtos.cs` | `ConditionDto`, `SaveConditionRequest`, `AllergyDto`, `SaveAllergyRequest`, `DietPlanDto`, `SaveDietPlanRequest`, `MedicalProfileDto {conditions, allergies, activeDiet}`, `MedicationWarningDto {kind: 'Allergy'|'Duplicate', message}` |
| Service | `MedicalProfileService.cs` | Đọc: `access.RequireAsync(user, residentId, ResidentSection.Health)`; ghi: Doctor/Nurse/Admin |
| Service (lõi, Huy sở hữu) | `MedicationService.CreateScheduleAsync` (trong `CareServices.cs`) | Gọi `CheckMedicationAsync`; có cảnh báo mà `request.AcknowledgeWarnings == false` → `BusinessRuleException("Cần xác nhận cảnh báo: ...")` |
| DTO (lõi, Huy sở hữu) | `CreateMedicationScheduleRequest` | thêm `public bool AcknowledgeWarnings { get; set; }` |
| Helper | `Services/Helpers/TextMatch.cs` | `Normalize(string)`: chữ thường + bỏ dấu tiếng Việt (`string.Normalize(FormD)` rồi bỏ `NonSpacingMark`, `đ→d`) |
| Controller | `MedicalProfileController.cs` | |

Quy tắc cảnh báo (`CheckMedicationAsync(residentId, medicationName)`):
- Với mỗi dị ứng `Type = Drug`: `Normalize(medicationName).Contains(Normalize(allergen))` → cảnh báo "Cư dân dị ứng <tác nhân> (<mức độ>): <phản ứng>".
- Lịch thuốc đang `IsActive` cùng `Normalize(name)` → cảnh báo trùng thuốc.
- Ghi rõ trong UI: **"Cảnh báo theo quy tắc so khớp tên, không thay thế kiểm tra của bác sĩ."**

## 3. API

| Method | Đường dẫn | Policy |
| --- | --- | --- |
| GET | `/api/residents/{id}/medical-profile` | ClinicalStaff (Family dùng endpoint dưới) |
| POST · PUT · DELETE | `/api/residents/{id}/conditions[/{conditionId}]` | Doctor, Nurse, Admin (policy mới `MedicalEditors` — PR lõi 1 dòng, hoặc kiểm role trong service) |
| POST · DELETE | `/api/residents/{id}/allergies[/{allergyId}]` | như trên |
| POST | `/api/residents/{id}/diet-plans` | như trên |
| POST | `/api/medication-schedules/check` `{residentId, medicationName}` | Prescribers → `MedicationWarningDto[]` |
| GET | `/api/family/residents/{id}/medical-profile` | FamilyOnly (link + `CanViewHealth`) |

## 4. Frontend

- `types/care.ts`, `api/medicalProfile.ts`, `utils/status.care.ts` (`allergySeverity`, `dietType`, `texture`).
- `pages/Staff/resident/ClinicalTabs.tsx`: tab **"Bệnh nền & dị ứng"** (2 bảng + modal thêm/sửa) và **"Chế độ ăn"** (chế độ hiện tại + lịch sử + nút "Đặt chế độ mới").
- Component `AllergyBadge` (trong `pages/Care/`) đặt cạnh tên ở `ResidentDetail.tsx` (1 dòng ở file lõi — nhắn Viên).
- `MedicationTab` (trong `ClinicalTabs.tsx`): trước khi lưu lịch thuốc gọi `/check`; có cảnh báo → `Modal.confirm` liệt kê, đồng ý thì gửi lại với `acknowledgeWarnings: true`.
- `FamilyResident.tsx` (tab Sức khỏe): khối "Bệnh nền, dị ứng, chế độ ăn".

## 5. Dữ liệu demo — mục 11

NH-0001: bệnh "Tăng huyết áp" (Active), dị ứng **Penicillin** (Drug, Severe, "phát ban"); NH-0004: chế độ ăn `Diabetic` + `Soft`; NH-0002: dị ứng "Tôm" (Food).

## 6. Kiểm thử

1. `doctor` nhập lịch "Amoxicillin/Penicillin 500mg" cho NH-0001 → modal cảnh báo; bỏ tick → không lưu; tick → lưu.
2. Gọi `POST /api/medication-schedules` bằng Swagger với thuốc trên, `acknowledgeWarnings: false` → 400.
3. `caregiver` sửa dị ứng → 403; xem → OK với NH-0001.
4. Tạo chế độ ăn mới cho NH-0004 → chế độ cũ có `EndDate` = hôm qua.
5. Unit test cho `TextMatch.Normalize("Pênicillin") == "penicillin"` và hàm so khớp.

## Tham khảo code có sẵn

`MedicationService.CreateScheduleAsync`, `ClinicalTabs.tsx` → `MedicationTab`, `ResidentAccessService`.

## Bẫy cần tránh

- So khớp phải bỏ dấu và không phân biệt hoa thường, nếu không "penicilin" sẽ lọt.
- Filtered unique index `UX_DietPlans_Active`: kết thúc plan cũ và thêm plan mới **trong cùng transaction**, nếu không sẽ vi phạm index.
