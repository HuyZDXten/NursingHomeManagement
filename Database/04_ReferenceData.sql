/* ============================================================
   NursingHome — 04_ReferenceData.sql
   ------------------------------------------------------------
   Dữ liệu danh mục cần cho MỌI môi trường: tài khoản Admin đầu tiên và
   danh mục gói dịch vụ. Dữ liệu demo (cư dân, hóa đơn...) nằm ở 05_DemoSeed.sql.

   Admin mặc định: admin@nursinghome.local / Demo@123456
   -> ĐỔI MẬT KHẨU ngay sau lần đăng nhập đầu (Hồ sơ > Đổi mật khẩu).
   Hash là BCrypt thật, sinh bằng chính hasher của project:
       cd BE && dotnet run --project NursingHome.API -- --hash-password "Demo@123456"
   Không tự bịa hash, không lưu mật khẩu dạng chữ.

   Idempotent: dòng đã có (theo Id) thì bỏ qua. Id cố định bằng IDENTITY_INSERT.
   Prerequisite: 01_Schema.sql.
   Chạy: sqlcmd -S localhost -d NursingHomeDB -E -C -b -I -f 65001 -i Database\04_ReferenceData.sql
   ============================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

DECLARE @Hash NVARCHAR(100) = N'$2a$11$H0uu9HfLJtT4Nw7VAhoTEuP3b3Z4mchJ4AN4HRsSU8BtThXSB2KFG'; -- Demo@123456

IF NOT EXISTS (SELECT 1 FROM dbo.Accounts WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.Accounts ON;
    INSERT INTO dbo.Accounts (Id, Email, PasswordHash, FullName, Phone, Role, IsActive)
    VALUES (1, N'admin@nursinghome.local', @Hash, N'Quản trị viên', N'0900000001', N'Admin', 1);
    SET IDENTITY_INSERT dbo.Accounts OFF;
END;

-- Danh mục gói dịch vụ (giá VND/tháng). Đổi giá ở đây không ảnh hưởng đăng ký đã chốt.
IF NOT EXISTS (SELECT 1 FROM dbo.ServicePackages WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.ServicePackages ON;
    INSERT INTO dbo.ServicePackages (Id, Name, MonthlyPrice, Description, IsActive) VALUES
    (1, N'Gói Cơ bản', 8000000, N'Phòng 2 giường, 3 bữa ăn/ngày, theo dõi sức khỏe hằng ngày, sinh hoạt tập thể.', 1),
    (2, N'Gói Tiêu chuẩn', 12000000, N'Như Gói Cơ bản + vật lý trị liệu 2 buổi/tuần, chế độ dinh dưỡng riêng.', 1),
    (3, N'Gói Cao cấp', 18000000, N'Phòng đơn, điều dưỡng theo dõi 24/7, vật lý trị liệu hằng ngày, khám định kỳ hằng tuần.', 1),
    (4, N'Gói Chăm sóc đặc biệt', 22000000, N'Dành cho người cao tuổi sa sút trí tuệ hoặc nằm lâu, hộ lý 1 kèm 1.', 1);
    SET IDENTITY_INSERT dbo.ServicePackages OFF;
END;

-- Danh mục ca trực (S3-V2). Giờ cơ sở UTC+7; ca đêm kết thúc sáng hôm sau (EndTime < StartTime).
IF NOT EXISTS (SELECT 1 FROM dbo.Shifts WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.Shifts ON;
    INSERT INTO dbo.Shifts (Id, Code, Name, StartTime, EndTime, IsActive) VALUES
    (1, N'MORNING', N'Ca sáng', '06:00', '14:00', 1),
    (2, N'AFTERNOON', N'Ca chiều', '14:00', '22:00', 1),
    (3, N'NIGHT', N'Ca đêm', '22:00', '06:00', 1);
    SET IDENTITY_INSERT dbo.Shifts OFF;
END;

-- Danh mục dịch vụ phát sinh (S3-V5). Đổi giá danh mục không ảnh hưởng lần sử dụng đã ghi (giá chốt lúc ghi).
IF NOT EXISTS (SELECT 1 FROM dbo.ExtraServices WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.ExtraServices ON;
    INSERT INTO dbo.ExtraServices (Id, Name, Unit, UnitPrice, IsActive) VALUES
    (1, N'Giặt ủi', N'kg', 30000, 1),
    (2, N'Vật lý trị liệu', N'buổi', 250000, 1),
    (3, N'Cắt tóc', N'lần', 50000, 1),
    (4, N'Đưa đón khám ngoài', N'chuyến', 300000, 1);
    SET IDENTITY_INSERT dbo.ExtraServices OFF;
END;

COMMIT TRANSACTION;
PRINT N'04_ReferenceData.sql: xong.';
GO
