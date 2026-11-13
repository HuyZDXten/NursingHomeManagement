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

PRINT N'03_StoredProcedures.sql: xong.';
GO
