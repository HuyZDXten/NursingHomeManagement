/* ============================================================
   NursingHome — 00_CreateDatabase.sql
   ------------------------------------------------------------
   Tạo database NursingHomeDB nếu chưa có. Không xóa, không ghi đè.
   Muốn dựng lại từ đầu: chạy setup-local-db.ps1 -Recreate.

   Chạy:  sqlcmd -S localhost -E -C -I -f 65001 -i Database\00_CreateDatabase.sql
   ============================================================ */
SET NOCOUNT ON;
GO

IF DB_ID(N'NursingHomeDB') IS NULL
BEGIN
    CREATE DATABASE NursingHomeDB COLLATE Vietnamese_CI_AS;
    PRINT N'Đã tạo database NursingHomeDB.';
END
ELSE
    PRINT N'NursingHomeDB đã tồn tại, bỏ qua.';
GO

ALTER DATABASE NursingHomeDB SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
