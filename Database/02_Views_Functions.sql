/* ============================================================
   NursingHome — 02_Views_Functions.sql
   ------------------------------------------------------------
   View phục vụ báo cáo/dashboard. CREATE OR ALTER nên chạy lại an toàn.
   Prerequisite: 01_Schema.sql.
   Chạy: sqlcmd -S localhost -d NursingHomeDB -E -C -b -I -f 65001 -i Database\02_Views_Functions.sql
   ============================================================ */
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Tổng tiền, đã thu, còn nợ của từng hóa đơn (làm tròn từng dòng tới đồng).
CREATE OR ALTER VIEW dbo.vw_InvoiceBalances
AS
SELECT
    i.Id AS InvoiceId,
    i.ResidentId,
    i.Number,
    i.BillingPeriod,
    i.DueDate,
    i.Status,
    ISNULL(items.Total, 0) AS Total,
    ISNULL(pays.Paid, 0) AS Paid,
    ISNULL(items.Total, 0) - ISNULL(pays.Paid, 0) AS Remaining
FROM dbo.Invoices AS i
OUTER APPLY (SELECT SUM(ROUND(it.Quantity * it.UnitPrice, 0)) AS Total FROM dbo.InvoiceItems AS it WHERE it.InvoiceId = i.Id) AS items
OUTER APPLY (SELECT SUM(p.Amount) AS Paid FROM dbo.Payments AS p WHERE p.InvoiceId = i.Id) AS pays;
GO

-- Công suất giường theo phòng.
CREATE OR ALTER VIEW dbo.vw_BedOccupancy
AS
SELECT
    r.Id AS RoomId,
    r.Number AS RoomNumber,
    r.Floor,
    r.IsActive,
    COUNT(b.Id) AS TotalBeds,
    SUM(CASE WHEN b.Status = N'Occupied' THEN 1 ELSE 0 END) AS OccupiedBeds,
    SUM(CASE WHEN b.Status = N'Available' AND r.IsActive = 1 THEN 1 ELSE 0 END) AS AvailableBeds,
    SUM(CASE WHEN b.Status = N'Maintenance' THEN 1 ELSE 0 END) AS MaintenanceBeds
FROM dbo.Rooms AS r
LEFT JOIN dbo.Beds AS b ON b.RoomId = r.Id
GROUP BY r.Id, r.Number, r.Floor, r.IsActive;
GO

PRINT N'02_Views_Functions.sql: xong.';
GO
