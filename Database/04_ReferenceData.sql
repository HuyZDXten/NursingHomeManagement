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

-- Tài liệu tri thức mẫu cho chatbot (S4-V1, tool search_knowledge). Admin sửa ở /staff/ai-knowledge.
-- Giá gói và số giường KHÔNG ghi ở đây: chatbot lấy trực tiếp từ dữ liệu (list_packages, get_bed_availability).
IF NOT EXISTS (SELECT 1 FROM dbo.AiKnowledgeDocs WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.AiKnowledgeDocs ON;
    INSERT INTO dbo.AiKnowledgeDocs (Id, Title, Content, Tags, IsPublic, IsActive) VALUES
    (1, N'Quy trình nhập viện',
        N'Bước 1: gia đình gửi yêu cầu tư vấn hoặc đặt lịch tham quan. Bước 2: tham quan cơ sở, gặp điều dưỡng trưởng để trao đổi nhu cầu chăm sóc. '
        + N'Bước 3: mang giấy tờ tùy thân của người cao tuổi, thẻ bảo hiểm y tế, giấy ra viện hoặc đơn thuốc gần nhất (nếu có). '
        + N'Bước 4: bác sĩ của cơ sở đánh giá sức khỏe ban đầu. Bước 5: ký hợp đồng, chọn gói dịch vụ, nhận giường và bàn giao đồ dùng cá nhân.',
        N'nhập viện, thủ tục, giấy tờ, hợp đồng, quy trình', 1, 1),
    (2, N'Giờ thăm và quy định khi thăm',
        N'Giờ thăm: 08:00–11:00 và 14:00–20:00 mỗi ngày. Mỗi lần tối đa 4 người thăm một cư dân; trẻ em dưới 12 tuổi đi cùng người lớn. '
        + N'Vui lòng đăng ký tại quầy lễ tân, không mang đồ ăn khi chưa hỏi điều dưỡng (nhiều cư dân ăn kiêng), không hút thuốc trong khuôn viên. '
        + N'Gia đình có thể đặt lịch thăm trước qua cổng gia đình để cơ sở chuẩn bị.',
        N'giờ thăm, thăm, quy định, thăm nuôi, lịch thăm', 1, 1),
    (3, N'Chính sách thanh toán',
        N'Hóa đơn phát hành vào đầu tháng, tính theo số ngày lưu trú thực tế của gói đã đăng ký cộng dịch vụ phát sinh (giặt ủi, vật lý trị liệu, cắt tóc, đưa đón khám). '
        + N'Hạn thanh toán ghi trên hóa đơn (thường ngày 10). Gia đình thanh toán bằng mã VietQR trên cổng gia đình, chuyển khoản hoặc tiền mặt tại quầy. '
        + N'Hệ thống nhắc trước hạn 3 ngày và khi quá hạn.',
        N'thanh toán, hóa đơn, chi phí, VietQR, chuyển khoản, tiền', 1, 1),
    (4, N'Dịch vụ y tế tại cơ sở',
        N'Có: bác sĩ khám định kỳ hàng tuần, điều dưỡng trực 24/7, theo dõi sinh hiệu hằng ngày, phát thuốc theo đơn của bác sĩ, vật lý trị liệu, đưa đón đi khám ngoài. '
        + N'Không có: phẫu thuật, chạy thận, chăm sóc cấp cứu chuyên sâu — khi cần cơ sở chuyển cư dân đến bệnh viện gần nhất và báo gia đình ngay.',
        N'y tế, bác sĩ, điều dưỡng, thuốc, cấp cứu, vật lý trị liệu, dịch vụ', 1, 1),
    (5, N'Câu hỏi thường gặp',
        N'Có thể mang đồ dùng cá nhân không? Có, nên ghi tên lên đồ dùng. Có wifi không? Có, miễn phí ở mọi tầng. '
        + N'Gia đình xem tình hình người thân thế nào? Qua cổng gia đình: sức khỏe, việc chăm sóc, thuốc, hóa đơn theo quyền được cấp, và nhắn tin với cơ sở. '
        + N'Có thể đổi gói dịch vụ không? Có, kết thúc gói cũ và đăng ký gói mới, tính tiền theo ngày.',
        N'hỏi đáp, faq, đồ dùng, wifi, đổi gói, cổng gia đình', 1, 1),
    (6, N'Quy trình nội bộ khi cư dân té ngã',
        N'Không di chuyển cư dân nếu nghi gãy xương; gọi điều dưỡng trực; đo sinh hiệu; báo bác sĩ; ghi sự cố trong hệ thống trong vòng 1 giờ; báo gia đình. '
        + N'Tài liệu nội bộ, chatbot công khai không dùng.',
        N'té ngã, sự cố, nội bộ', 0, 1);
    SET IDENTITY_INSERT dbo.AiKnowledgeDocs OFF;
END;

COMMIT TRANSACTION;
PRINT N'04_ReferenceData.sql: xong.';
GO
