# S3-V3 · Báo cáo & xuất Excel (UC-122, UC-123, UC-124, UC-125, UC-128)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Viên | Must (UC-123) / Should | 5 | 3.1 (doanh thu, công suất) – 3.2 (thuốc, sự cố) | UC-125 cần bảng `Incidents` của S3-H1 | `feature/s3-v3-reports` |

## Mục tiêu

Màn "Báo cáo" có 4 báo cáo truy vấn SQL thật, có biểu đồ và nút xuất Excel: doanh thu & công nợ (Kế toán), công suất giường theo tháng (Admin/Lễ tân), tuân thủ dùng thuốc (Bác sĩ/Admin), sự cố theo loại/tháng (Điều dưỡng/Admin).

## Tiêu chí hoàn thành

- [ ] **Doanh thu & công nợ** (UC-123, Must): chọn năm → cột thực thu theo tháng (tách PayOS/thủ công), bảng công nợ các hóa đơn còn nợ (cư dân, số, kỳ, hạn, còn nợ, số ngày quá hạn).
- [ ] **Công suất giường** (UC-122): chọn năm → % công suất theo tháng = số giường-ngày có người / số giường-ngày khả dụng.
- [ ] **Tuân thủ thuốc** (UC-124): khoảng ngày → theo cư dân: số liều Taken/Missed/Refused, tỉ lệ Taken.
- [ ] **Sự cố** (UC-125, làm sau khi S3-H1 merge): khoảng ngày → số sự cố theo loại × mức độ, theo tháng.
- [ ] Mỗi báo cáo có nút **Xuất Excel** (`.xlsx`, tiêu đề tiếng Việt, định dạng tiền `#,##0`).
- [ ] Phân quyền đúng từng báo cáo; Caregiver/Family → 403.

## 1. Database — `03_StoredProcedures.sql` (file của Viên)

`sp_MonthlyRevenue @Year` đã có. Thêm:

```sql
-- UC-122: công suất giường theo tháng (giờ VN). Giường-ngày có người / giường-ngày của phòng đang dùng.
CREATE OR ALTER PROCEDURE dbo.sp_OccupancyByMonth @Year INT
AS
BEGIN
    SET NOCOUNT ON;
    WITH Months AS (
        SELECT 1 AS M UNION ALL SELECT M + 1 FROM Months WHERE M < 12
    ), Periods AS (
        SELECT M, DATEFROMPARTS(@Year, M, 1) AS FromDate, EOMONTH(DATEFROMPARTS(@Year, M, 1)) AS ToDate FROM Months
    )
    SELECT p.M AS [Month],
           (SELECT COUNT(*) FROM dbo.Beds b JOIN dbo.Rooms r ON r.Id = b.RoomId WHERE r.IsActive = 1) * DAY(p.ToDate) AS AvailableBedDays,
           ISNULL((SELECT SUM(DATEDIFF(DAY,
                       CASE WHEN CAST(DATEADD(HOUR, 7, a.StartedAt) AS DATE) > p.FromDate THEN CAST(DATEADD(HOUR, 7, a.StartedAt) AS DATE) ELSE p.FromDate END,
                       CASE WHEN a.EndedAt IS NULL OR CAST(DATEADD(HOUR, 7, a.EndedAt) AS DATE) > p.ToDate THEN p.ToDate ELSE CAST(DATEADD(HOUR, 7, a.EndedAt) AS DATE) END) + 1)
                   FROM dbo.BedAssignments a
                   WHERE CAST(DATEADD(HOUR, 7, a.StartedAt) AS DATE) <= p.ToDate
                     AND (a.EndedAt IS NULL OR CAST(DATEADD(HOUR, 7, a.EndedAt) AS DATE) >= p.FromDate)), 0) AS OccupiedBedDays
    FROM Periods p
    ORDER BY p.M;
END;
GO

-- UC-124: tuân thủ dùng thuốc theo cư dân.
CREATE OR ALTER PROCEDURE dbo.sp_MedicationAdherence @From DATE, @To DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.Id AS ResidentId, r.Code, r.FullName,
           SUM(CASE WHEN l.Status = N'Taken' THEN 1 ELSE 0 END) AS Taken,
           SUM(CASE WHEN l.Status = N'Missed' THEN 1 ELSE 0 END) AS Missed,
           SUM(CASE WHEN l.Status = N'Refused' THEN 1 ELSE 0 END) AS Refused
    FROM dbo.MedicationLogs l
    JOIN dbo.MedicationSchedules s ON s.Id = l.MedicationScheduleId
    JOIN dbo.Residents r ON r.Id = s.ResidentId
    WHERE CAST(DATEADD(HOUR, 7, l.ScheduledAt) AS DATE) BETWEEN @From AND @To
    GROUP BY r.Id, r.Code, r.FullName
    ORDER BY r.Code;
END;
GO
```

UC-125 (sau S3-H1): `sp_IncidentSummary @From, @To` — `GROUP BY Type, Severity, YEAR/MONTH(OccurredAt +7h)`; tên cột lấy theo bảng `Incidents` của Huy (xem [S3-H1](S3-H1-su-co.md)).

Công nợ dùng view có sẵn `vw_InvoiceBalances` (`WHERE Remaining > 0 AND Status IN (N'Unpaid', N'PartiallyPaid')`).

## 2. Backend

| Layer | File | Nội dung |
| --- | --- | --- |
| DAO | `ReportDAO.cs` | Gọi proc/view bằng `context.Database.SqlQuery<T>($"EXEC dbo.sp_MonthlyRevenue {year}").ToListAsync()` (EF Core 8+ hỗ trợ kiểu không phải entity; tham số nội suy được đổi thành SqlParameter nên an toàn). Kiểu kết quả là record trong DAO (`RevenueRow`, `OccupancyRow`, `AdherenceRow`, `DebtRow`, `IncidentSummaryRow`). |
| DTOs | `DTOs/Reports/ReportDtos.cs` | `RevenueReportDto {months[], totalCollected, debts[]}`, `OccupancyReportDto {months[{month, percent, occupied, available}]}`, `AdherenceRowDto {…, takenRate}`, `IncidentSummaryDto` |
| Service | `ReportService.cs` | Tính %, số ngày quá hạn (`clock.Today - DueDate`), sắp xếp |
| Excel | `Services/Helpers/ExcelExporter.cs` | Package **ClosedXML** (MIT) thêm vào `NursingHome.Services.csproj`. Hàm chung `byte[] Export<T>(string sheet, IEnumerable<T> rows, (string Header, Func<T, object?> Value, string? Format)[] columns)` |
| Controller | `ReportsController.cs` | JSON + `/export` trả `File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "doanh-thu-2026.xlsx")` |

## 3. API

| Method | Đường dẫn | Policy |
| --- | --- | --- |
| GET | `/api/reports/revenue?year=` · `/api/reports/revenue/export?year=` | FinanceStaff |
| GET | `/api/reports/occupancy?year=` · `/export` | BillingStaff |
| GET | `/api/reports/medication-adherence?from=&to=` · `/export` | Prescribers (Admin, Doctor) |
| GET | `/api/reports/incidents?from=&to=` · `/export` | CarePlanners (Admin, Nurse) |

## 4. Frontend

- `src/pages/Operations/Reports.tsx`: `Tabs` — chỉ hiện tab hợp role (`hasRole`). Mỗi tab: bộ lọc (năm hoặc khoảng ngày) + biểu đồ Recharts (`BarChart` cho doanh thu/sự cố, `LineChart` cho công suất %) + `Table` + nút **Xuất Excel**.
- Tải file: `apiClient.get(url, { responseType: 'blob' })` rồi tạo link bằng `URL.createObjectURL` (đặt hàm `downloadFile(url, fileName)` trong `src/api/reports.ts`).
- Route `/staff/reports` (STAFF; từng tab tự lọc role) + menu "Báo cáo" trong `src/modules/operations.tsx`.

## 5. Dữ liệu demo

Thêm vài `Payments` ở 3–4 tháng trước (mục 10, Id 100+) để biểu đồ doanh thu không trống; thêm `MedicationLogs` cho 7 ngày qua (Taken/Missed/Refused lẫn lộn).

## 6. Kiểm thử

1. `accountant`: tab Doanh thu có cột tháng hiện tại khớp màn Dashboard (thực thu); xuất Excel mở được, số tiền đúng định dạng.
2. `doctor`: chỉ thấy tab Tuân thủ thuốc; tỉ lệ Taken của NH-0001 khớp lịch sử thuốc trong hồ sơ.
3. `caregiver` gọi `/api/reports/revenue` → 403.
4. Đối chiếu `sp_OccupancyByMonth` bằng tay cho tháng hiện tại (4 giường có người / 8 giường).

## Tham khảo code có sẵn

`DashboardDAO.CollectedByMonthAsync`, `pages/Admin/Dashboard.tsx` (Recharts), `vw_InvoiceBalances` trong `02_Views_Functions.sql`.

## Bẫy cần tránh

- Luôn quy đổi `DATETIME2` UTC sang ngày VN (`DATEADD(HOUR, 7, ...)`) trước khi gom theo tháng.
- `SqlQuery<T>` yêu cầu tên cột trả về khớp tên thuộc tính của `T`.
- Không ghép chuỗi SQL bằng tay từ input người dùng.
