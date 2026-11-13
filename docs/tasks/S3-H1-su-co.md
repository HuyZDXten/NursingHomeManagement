# S3-H1 · Báo cáo & xử lý sự cố (UC-58, UC-59, UC-107)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Huy | Must | 5 | 3.1 (task đầu tiên — làm trước) | — | `feature/s3-h1-incidents` |

## Mục tiêu

Hộ lý/điều dưỡng báo sự cố (té ngã, chấn thương, hành vi, khác) ngay khi xảy ra; sự cố mức **Cao** báo ngay cho điều dưỡng, bác sĩ, admin. Điều dưỡng/Admin xử lý và đóng sự cố, có thể báo cho gia đình. Đây là task đầu của Huy: làm **đủ mọi layer** theo đúng ví dụ ở [HANDOVER.md](../HANDOVER.md) mục 5 để quen khuôn mẫu.

## Tiêu chí hoàn thành

- [ ] `caregiver` báo sự cố cho cư dân mình đang được giao (NH-0001/0002): loại, mức độ, thời điểm (không ở tương lai), vị trí, mô tả, xử trí ban đầu.
- [ ] `caregiver` báo sự cố cho NH-0003 (không được giao) → 403.
- [ ] Mức **High** → thông báo ngay cho mọi Nurse, Doctor, Admin (chuông realtime).
- [ ] Nurse/Admin đổi trạng thái Open → Investigating → Closed; đóng bắt buộc nhập kết luận.
- [ ] Khi đóng có tùy chọn "Báo gia đình" → gia đình có quyền `CanViewCare` nhận thông báo (UC-107) và thấy sự cố trong trang người thân (chỉ những sự cố đã đánh dấu chia sẻ).
- [ ] Trang `/staff/incidents`: lọc trạng thái/mức độ/khoảng ngày; tab "Sự cố" trong hồ sơ cư dân.
- [ ] Cột của bảng `Incidents` giữ đúng tên như dưới (Viên dùng cho báo cáo UC-125).

## 1. Database — `01_Schema.sql` mục 10 (chèn trên dòng KẾT THÚC)

```sql
IF OBJECT_ID(N'dbo.Incidents', N'U') IS NULL
CREATE TABLE dbo.Incidents (
    Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Incidents PRIMARY KEY,
    ResidentId       INT            NOT NULL CONSTRAINT FK_Incidents_Residents REFERENCES dbo.Residents (Id),
    Type             NVARCHAR(20)   NOT NULL,
    Severity         NVARCHAR(10)   NOT NULL,
    OccurredAt       DATETIME2      NOT NULL,
    Location         NVARCHAR(100)  NULL,
    Description      NVARCHAR(2000) NOT NULL,
    ImmediateAction  NVARCHAR(2000) NULL,
    ReportedById     INT            NOT NULL CONSTRAINT FK_Incidents_Reporter REFERENCES dbo.Accounts (Id),
    Status           NVARCHAR(20)   NOT NULL CONSTRAINT DF_Incidents_Status DEFAULT (N'Open'),
    Resolution       NVARCHAR(2000) NULL,
    ClosedById       INT            NULL CONSTRAINT FK_Incidents_Closer REFERENCES dbo.Accounts (Id),
    ClosedAt         DATETIME2      NULL,
    ShareWithFamily  BIT            NOT NULL CONSTRAINT DF_Incidents_Share DEFAULT (0),
    FamilyNotifiedAt DATETIME2      NULL,
    RowVersion       ROWVERSION     NOT NULL,
    CreatedAt        DATETIME2      NOT NULL CONSTRAINT DF_Incidents_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_Incidents_Type CHECK (Type IN (N'Fall', N'Injury', N'Behavior', N'Other')),
    CONSTRAINT CK_Incidents_Severity CHECK (Severity IN (N'Low', N'Medium', N'High')),
    CONSTRAINT CK_Incidents_Status CHECK (Status IN (N'Open', N'Investigating', N'Closed')),
    CONSTRAINT CK_Incidents_Closed CHECK (Status <> N'Closed' OR (Resolution IS NOT NULL AND ClosedAt IS NOT NULL))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Incidents_Resident_OccurredAt')
    CREATE INDEX IX_Incidents_Resident_OccurredAt ON dbo.Incidents (ResidentId, OccurredAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Incidents_Status')
    CREATE INDEX IX_Incidents_Status ON dbo.Incidents (Status, OccurredAt DESC);
GO
```

Chạy `.\Database\setup-local-db.ps1` (không `-Recreate`): bảng mới được tạo, dữ liệu cũ giữ nguyên.

## 2. Backend — từng bước

**2.1 Entity** — `BE/NursingHome.BusinessObjects/Entities/CareIncidents.cs`:

```csharp
namespace NursingHome.BusinessObjects.Entities;

public enum IncidentType { Fall, Injury, Behavior, Other }
public enum IncidentSeverity { Low, Medium, High }
public enum IncidentStatus { Open, Investigating, Closed }

public class Incident
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public Resident Resident { get; set; } = null!;
    public IncidentType Type { get; set; }
    public IncidentSeverity Severity { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? Location { get; set; }
    public string Description { get; set; } = "";
    public string? ImmediateAction { get; set; }
    public int ReportedById { get; set; }
    public Account ReportedBy { get; set; } = null!;
    public IncidentStatus Status { get; set; } = IncidentStatus.Open;
    public string? Resolution { get; set; }
    public int? ClosedById { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool ShareWithFamily { get; set; }
    public DateTime? FamilyNotifiedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

**2.2 Mapping** — `NursingHomeContext.Care.cs`:

```csharp
public DbSet<Incident> Incidents => Set<Incident>();

partial void ConfigureCare(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Incident>(entity =>
    {
        entity.ToTable("Incidents");
        entity.Property(e => e.Type).HasConversion<string>();
        entity.Property(e => e.Severity).HasConversion<string>();
        entity.Property(e => e.Status).HasConversion<string>();
        entity.Property(e => e.RowVersion).IsRowVersion();
        entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
        entity.HasOne(e => e.ReportedBy).WithMany().HasForeignKey(e => e.ReportedById);
    });
}
```

**2.3 DAO** — `BE/NursingHome.DataAccessObjects/IncidentDAO.cs`: `IncidentDAO : GenericDAO<Incident>` (singleton `Instance`) với
`ListAsync(int? residentId, IReadOnlyCollection<int>? onlyResidentIds, IncidentStatus? status, DateTime? fromUtc, DateTime? toUtc)` (Include Resident, ReportedBy),
`GetDetailAsync(id)`,
`ListSharedForResidentAsync(residentId, take)` (cho gia đình),
`UpdateStatusAsync(id, byte[] rowVersion, Action<Incident> apply)` (đặt `OriginalValue` của RowVersion như `CareDAO.UpdateStatusAsync`).

**2.4 Repository** — `IIncidentRepository` + `IncidentRepository` (mỗi method một dòng gọi DAO).

**2.5 DTOs** — `BE/NursingHome.DTOs/Care/IncidentDtos.cs`: `IncidentDto` (id, residentId, residentName, residentCode, type, severity, occurredAt, location, description, immediateAction, reportedByName, status, resolution, closedAt, shareWithFamily, rowVersion), `ReportIncidentRequest` (`[Range(1,int.MaxValue)] ResidentId`, `[Required] Type`, `[Required] Severity`, `OccurredAt`, `[MaxLength(100)] Location`, `[Required, MaxLength(2000)] Description`, `ImmediateAction`), `UpdateIncidentRequest {status, resolution, shareWithFamily, notifyFamily, rowVersion}`.

**2.6 Service** — interface `IIncidentService` trong `ICareModuleServices.cs`, cài đặt `Implementations/IncidentService.cs`:

```csharp
public async Task<IncidentDto> ReportAsync(CurrentUser user, ReportIncidentRequest request)
{
    await access.RequireAsync(user, request.ResidentId, ResidentSection.Care);   // Caregiver: chỉ cư dân được giao
    if (request.OccurredAt > clock.UtcNow.AddMinutes(5)) throw new BusinessRuleException("Thời điểm xảy ra không được ở tương lai.");
    // parse Type/Severity bằng Enum.TryParse, sai → BusinessRuleException
    var saved = await incidents.AddAsync(new Incident { ..., ReportedById = user.AccountId, CreatedAt = clock.UtcNow });
    if (saved.Severity == IncidentSeverity.High)
        await notifications.NotifyRolesAsync([Roles.Nurse, Roles.Doctor, Roles.Admin], NotificationType.Care,
            $"Sự cố mức CAO: {resident.FullName}", request.Description, $"/staff/incidents?id={saved.Id}");
    return ...;
}
```

Quy tắc khác:
- `ListAsync(user, filter)`: Caregiver chỉ thấy cư dân trong `access.VisibleResidentIdsAsync(user)`.
- `UpdateAsync`: chỉ Nurse/Admin/Doctor; chuyển trạng thái `Open → Investigating → Closed` (cho phép `Open → Closed`); `Closed` là cuối, không mở lại. Đóng bắt buộc `Resolution`, đặt `ClosedById`, `ClosedAt`.
- `notifyFamily = true` khi đóng (bắt buộc `shareWithFamily = true`): `residents.FamilyAccountIdsAsync(...)` là của Billing — **viết thêm** trong DAO của mình hàm lấy Family có `CanViewCare` (đừng sửa `ResidentDAO` lõi), gửi `NotifyAsync(..., NotificationType.Care, "Thông báo sự cố của <tên>", "<loại> — đã xử lý: <kết luận>", "/family/residents/<id>")`, ghi `FamilyNotifiedAt`.
- Map entity → DTO trong `CareMapper` (`Helpers/ModuleMappers.cs`).

**2.7 Đăng ký DI** — `ServiceCollectionExtensions.Care.cs`:

```csharp
services.AddScoped<IIncidentRepository, IncidentRepository>();
services.AddScoped<IIncidentService, IncidentService>();
```

**2.8 Controller** — `BE/NursingHome.API/Controllers/IncidentsController.cs` (xem bảng API).

**2.9 Gia đình (UC-107)** — `FamilyResidentDetailDto` là DTO lõi; **không sửa**. Thêm endpoint riêng `GET /api/family/residents/{id}/incidents` trong controller của mình (policy FamilyOnly, kiểm link + `CanViewCare`, chỉ `ShareWithFamily = 1`).

## 3. API

| Method | Đường dẫn | Policy | Ghi chú |
| --- | --- | --- | --- |
| GET | `/api/incidents?status=&severity=&from=&to=&residentId=` | ClinicalStaff | Caregiver bị lọc theo cư dân được giao |
| GET | `/api/incidents/{id}` | ClinicalStaff | |
| POST | `/api/incidents` | ClinicalStaff | Báo sự cố |
| POST | `/api/incidents/{id}/updates` | CarePlanners + Doctor (thêm policy hoặc kiểm role trong service) | Đổi trạng thái/đóng |
| GET | `/api/family/residents/{id}/incidents` | FamilyOnly | Chỉ sự cố chia sẻ |

## 4. Frontend

- `src/types/care.ts`: `Incident`, `IncidentType`, `IncidentSeverity`, `IncidentStatus`.
- `src/api/incidents.ts`: `list`, `get`, `report`, `update`, `familyIncidents`.
- `src/utils/status.care.ts`: `incident` (Open đỏ, Investigating xanh, Closed xám), `incidentSeverity` (Low/Medium/High), `incidentType` (Té ngã, Chấn thương, Hành vi, Khác).
- `src/pages/Care/Incidents.tsx`: bảng + bộ lọc + nút "Báo sự cố" (modal: `ResidentSelect`, `Select` loại/mức độ, `DatePicker showTime` mặc định bây giờ, vị trí, mô tả, xử trí ban đầu) + `Drawer` chi tiết với nút cập nhật trạng thái (modal: trạng thái, kết luận, `Checkbox` chia sẻ + báo gia đình).
- `src/pages/Staff/resident/ClinicalTabs.tsx`: thêm `IncidentsTab` (danh sách sự cố của cư dân + nút báo) và 1 dòng tab trong `ResidentDetail.tsx` (file lõi — nhắn Viên).
- `src/pages/Family/FamilyResident.tsx`: trong tab "Chăm sóc hằng ngày", thêm danh sách sự cố đã chia sẻ (gọi endpoint gia đình).
- `src/modules/care.tsx`: route `/staff/incidents` (CLINICAL_STAFF), menu "Sự cố" (icon `WarningOutlined`).

## 5. Dữ liệu demo — `05_DemoSeed.sql` mục 11

```sql
IF NOT EXISTS (SELECT 1 FROM dbo.Incidents WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.Incidents ON;
    INSERT INTO dbo.Incidents (Id, ResidentId, Type, Severity, OccurredAt, Location, Description, ImmediateAction, ReportedById, Status, Resolution, ClosedById, ClosedAt, ShareWithFamily) VALUES
    (1, 2, N'Fall', N'Medium', DATEADD(DAY, -3, DATEADD(HOUR, 3, @TodayUtc)), N'Phòng 101', N'Bà trượt chân khi đứng dậy, không chấn thương (mô phỏng).', N'Đỡ dậy, kiểm tra, theo dõi 2 giờ.', 6, N'Closed', N'Đã lắp tay vịn cạnh giường.', 5, DATEADD(DAY, -2, @TodayUtc), 1),
    (2, 1, N'Behavior', N'Low', DATEADD(HOUR, 2, @TodayUtc), N'Phòng sinh hoạt chung', N'Ông lo lắng, đòi về nhà (mô phỏng).', N'Trò chuyện, gọi người nhà.', 6, N'Open', NULL, NULL, NULL, 0);
    SET IDENTITY_INSERT dbo.Incidents OFF;
END;
```

## 6. Kiểm thử

Thủ công:
1. `caregiver` báo sự cố High cho NH-0001 → `nurse` (cửa sổ khác) thấy chuông ngay.
2. `caregiver` báo cho NH-0003 → 403.
3. `nurse` đóng sự cố có tick "báo gia đình" → `family` thấy chuông + sự cố trong trang người thân; `family2` không thấy.
4. `caregiver` thử đóng sự cố bằng Swagger → 403.

Thêm vào `scripts/smoke-test.mjs` (đặt sau phần Sprint 1, dùng biến `caregiver`, `nurse` đã có):

```js
const inc = await call('POST', '/api/incidents', caregiver.accessToken, { residentId: 2, type: 'Fall', severity: 'High',
  occurredAt: new Date().toISOString(), description: 'Test té ngã' });
check('Caregiver báo sự cố cư dân được giao', inc.status === 200);
check('Nurse nhận cảnh báo sự cố mức cao',
  (await call('GET', '/api/notifications', nurse.accessToken)).data.items.some((n) => n.title.includes('Sự cố mức CAO')));
check('Caregiver báo sự cố cư dân không được giao -> 403',
  (await call('POST', '/api/incidents', caregiver.accessToken, { residentId: 3, type: 'Fall', severity: 'Low', occurredAt: new Date().toISOString(), description: 'x' })).status === 403);
```

## Tham khảo code có sẵn

- Toàn bộ khuôn mẫu: `CareTaskService` + `CareDAO` + `CareTasksController` + `pages/Staff/CareTasks.tsx` + `components/CareTaskModals.tsx`.
- Kiểm quyền theo cư dân: `HealthService.RecordAsync`. Thông báo theo role: `HealthService` (cảnh báo sinh hiệu).

## Bẫy cần tránh

- Thời điểm từ FE là ISO có `Z` (UTC) — lưu thẳng, đừng cộng/trừ 7 giờ ở BE.
- Không sửa `FamilyResidentDetailDto`/`FamilyService` (lõi); dùng endpoint riêng.
- `UpdateAsync` phải gửi kèm `rowVersion` (2 điều dưỡng cùng đóng → người sau nhận 409).
