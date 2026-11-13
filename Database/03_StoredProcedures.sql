/* ============================================================
   NursingHome — 03_StoredProcedures.sql (tùy chọn)
   ------------------------------------------------------------
   Thủ tục cho báo cáo nặng (Sprint 3: UC-122..UC-128). Dùng CREATE OR ALTER.
   Prerequisite: 01_Schema.sql, 02_Views_Functions.sql.
   Chạy: sqlcmd -S localhost -d NursingHomeDB -E -C -b -I -f 65001 -i Database\03_StoredProcedures.sql
   ============================================================ */
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Doanh thu thực thu theo tháng (theo giờ cơ sở UTC+7). Dùng cho UC-123.
CREATE OR ALTER PROCEDURE dbo.sp_MonthlyRevenue
    @Year INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        MONTH(DATEADD(HOUR, 7, p.PaidAt)) AS [Month],
        SUM(p.Amount) AS Collected,
        SUM(CASE WHEN p.Method = N'PayOS' THEN p.Amount ELSE 0 END) AS CollectedOnline,
        COUNT(*) AS PaymentCount
    FROM dbo.Payments AS p
    WHERE YEAR(DATEADD(HOUR, 7, p.PaidAt)) = @Year
    GROUP BY MONTH(DATEADD(HOUR, 7, p.PaidAt))
    ORDER BY [Month];
END;
GO

-- UC-122: công suất giường theo tháng (giờ cơ sở). Giường-ngày có người / giường-ngày của phòng đang dùng.
-- Tháng hiện tại chỉ tính tới hôm nay; tháng chưa tới trả 0 (không coi lượt ở đang mở là "đã ở" trong tương lai).
CREATE OR ALTER PROCEDURE dbo.sp_OccupancyByMonth
    @Year INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Today DATE = CAST(DATEADD(HOUR, 7, SYSUTCDATETIME()) AS DATE);
    DECLARE @Beds INT = (SELECT COUNT(*) FROM dbo.Beds AS b JOIN dbo.Rooms AS r ON r.Id = b.RoomId WHERE r.IsActive = 1);

    WITH Months AS (
        SELECT 1 AS M UNION ALL SELECT M + 1 FROM Months WHERE M < 12
    ), Periods AS (
        SELECT M,
               DATEFROMPARTS(@Year, M, 1) AS FromDate,
               CASE WHEN EOMONTH(DATEFROMPARTS(@Year, M, 1)) > @Today THEN @Today ELSE EOMONTH(DATEFROMPARTS(@Year, M, 1)) END AS ToDate
        FROM Months
    ), Stays AS (
        SELECT CAST(DATEADD(HOUR, 7, a.StartedAt) AS DATE) AS StartDate,
               CASE WHEN a.EndedAt IS NULL THEN NULL ELSE CAST(DATEADD(HOUR, 7, a.EndedAt) AS DATE) END AS EndDate
        FROM dbo.BedAssignments AS a
    )
    SELECT p.M AS [Month],
           CASE WHEN p.ToDate < p.FromDate THEN 0 ELSE @Beds * (DATEDIFF(DAY, p.FromDate, p.ToDate) + 1) END AS AvailableBedDays,
           ISNULL(SUM(CASE WHEN s.StartDate IS NULL THEN 0 ELSE DATEDIFF(DAY,
                        CASE WHEN s.StartDate > p.FromDate THEN s.StartDate ELSE p.FromDate END,
                        CASE WHEN s.EndDate IS NULL OR s.EndDate > p.ToDate THEN p.ToDate ELSE s.EndDate END) + 1 END), 0) AS OccupiedBedDays
    FROM Periods AS p
    LEFT JOIN Stays AS s
        ON p.ToDate >= p.FromDate
       AND s.StartDate <= p.ToDate
       AND (s.EndDate IS NULL OR s.EndDate >= p.FromDate)
    GROUP BY p.M, p.FromDate, p.ToDate
    ORDER BY p.M;
END;
GO

-- UC-124: tuân thủ dùng thuốc theo cư dân trong [@From, @To] (ngày theo giờ cơ sở). Liều chưa ghi nhận (Scheduled) không tính.
CREATE OR ALTER PROCEDURE dbo.sp_MedicationAdherence
    @From DATE,
    @To DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.Id AS ResidentId, r.Code, r.FullName,
           SUM(CASE WHEN l.Status = N'Taken' THEN 1 ELSE 0 END) AS Taken,
           SUM(CASE WHEN l.Status = N'Missed' THEN 1 ELSE 0 END) AS Missed,
           SUM(CASE WHEN l.Status = N'Refused' THEN 1 ELSE 0 END) AS Refused
    FROM dbo.MedicationLogs AS l
    JOIN dbo.MedicationSchedules AS s ON s.Id = l.MedicationScheduleId
    JOIN dbo.Residents AS r ON r.Id = s.ResidentId
    WHERE CAST(DATEADD(HOUR, 7, l.ScheduledAt) AS DATE) BETWEEN @From AND @To
      AND l.Status <> N'Scheduled'
    GROUP BY r.Id, r.Code, r.FullName
    ORDER BY r.Code;
END;
GO

-- UC-125 (sau khi S3-H1 có bảng Incidents): dbo.sp_IncidentSummary @From, @To — xem docs/tasks/S3-V3-bao-cao.md.

PRINT N'03_StoredProcedures.sql: xong.';
GO
