/* ============================================================
   NursingHome — 05_DemoSeed.sql  (CHỈ dùng cho máy local / demo)
   ------------------------------------------------------------
   Một tài khoản cho mỗi role + kịch bản demo đủ cho mọi màn hình:
   cư dân, phòng/giường, việc chăm sóc hôm nay, sinh hiệu, lịch thuốc,
   gói dịch vụ, hóa đơn tháng trước (đã trả / trả một phần / chưa trả để thử PayOS).

   Mật khẩu mọi tài khoản demo: Demo@123456 (hash BCrypt thật, xem 04_ReferenceData.sql)
     admin@nursinghome.local        Admin           (tạo ở 04_ReferenceData.sql)
     receptionist@nursinghome.local Receptionist
     accountant@nursinghome.local   Accountant
     doctor@nursinghome.local       Doctor
     nurse@nursinghome.local        Nurse
     caregiver@nursinghome.local    Caregiver (được giao NH-0001, NH-0002)
     caregiver2@nursinghome.local   Caregiver (được giao NH-0004)
     family@nursinghome.local       Family của NH-0001 (đủ 4 quyền)
     family2@nursinghome.local      Family của NH-0002 (không xem thuốc) — hóa đơn chưa trả để thử PayOS

   Ngày tháng tính theo "hôm nay" (giờ VN) lúc chạy script, nên demo ngày nào cũng đúng.
   Thuốc/sinh hiệu là dữ liệu MÔ PHỎNG, không dùng cho điều trị.

   Quy tắc: khi schema có cột mới mà dữ liệu demo cần, thêm giá trị vào INSERT của mục tương ứng
   ngay trong file này (không tạo file seed thứ hai, không seed bằng C#).

   Idempotent: mỗi mục kiểm tra theo Id đã có thì bỏ qua. Id cố định bằng IDENTITY_INSERT.
   Prerequisite: 01_Schema.sql, 04_ReferenceData.sql.
   Chạy: sqlcmd -S localhost -d NursingHomeDB -E -C -b -I -f 65001 -i Database\05_DemoSeed.sql
   ============================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

DECLARE @Hash NVARCHAR(100) = N'$2a$11$H0uu9HfLJtT4Nw7VAhoTEuP3b3Z4mchJ4AN4HRsSU8BtThXSB2KFG'; -- Demo@123456
DECLARE @NowUtc DATETIME2 = SYSUTCDATETIME();
DECLARE @Today DATE = CAST(DATEADD(HOUR, 7, @NowUtc) AS DATE);                  -- hôm nay theo giờ VN
DECLARE @TodayUtc DATETIME2 = DATEADD(HOUR, -7, CAST(@Today AS DATETIME2));      -- 00:00 giờ VN, quy ra UTC
DECLARE @PrevMonth DATE = DATEFROMPARTS(YEAR(DATEADD(MONTH, -1, @Today)), MONTH(DATEADD(MONTH, -1, @Today)), 1);
DECLARE @PrevMonthEnd DATE = EOMONTH(@PrevMonth);
DECLARE @PrevDays INT = DAY(@PrevMonthEnd);
DECLARE @ThisMonth DATE = DATEADD(MONTH, 1, @PrevMonth);

/* ---------------- 1. Tài khoản demo ---------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Accounts WHERE Id = 2)
BEGIN
    SET IDENTITY_INSERT dbo.Accounts ON;
    INSERT INTO dbo.Accounts (Id, Email, PasswordHash, FullName, Phone, Role, IsActive) VALUES
    (2, N'receptionist@nursinghome.local', @Hash, N'Lê Thị Lan', N'0900000002', N'Receptionist', 1),
    (3, N'accountant@nursinghome.local', @Hash, N'Phạm Minh Đức', N'0900000003', N'Accountant', 1),
    (4, N'doctor@nursinghome.local', @Hash, N'BS. Nguyễn Văn Hùng', N'0900000004', N'Doctor', 1),
    (5, N'nurse@nursinghome.local', @Hash, N'ĐD. Trần Thị Mai', N'0900000005', N'Nurse', 1),
    (6, N'caregiver@nursinghome.local', @Hash, N'Hoàng Văn Nam', N'0900000006', N'Caregiver', 1),
    (7, N'caregiver2@nursinghome.local', @Hash, N'Đỗ Thị Hoa', N'0900000007', N'Caregiver', 1),
    (8, N'family@nursinghome.local', @Hash, N'Nguyễn Thị Bích', N'0900000008', N'Family', 1),
    (9, N'family2@nursinghome.local', @Hash, N'Trần Quốc Bảo', N'0900000009', N'Family', 1);
    SET IDENTITY_INSERT dbo.Accounts OFF;
END;

/* ---------------- 2. Phòng & giường ---------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Rooms WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.Rooms ON;
    INSERT INTO dbo.Rooms (Id, Number, Floor, Description, IsActive) VALUES
    (1, N'101', 1, N'Phòng đôi, gần phòng điều dưỡng', 1),
    (2, N'102', 1, N'Phòng đôi, hướng vườn', 1),
    (3, N'201', 2, N'Phòng đơn VIP, có ban công', 1),
    (4, N'202', 2, N'Phòng 3 giường', 1);
    SET IDENTITY_INSERT dbo.Rooms OFF;

    SET IDENTITY_INSERT dbo.Beds ON;
    INSERT INTO dbo.Beds (Id, RoomId, Number, Status) VALUES
    (1, 1, N'A', N'Occupied'),
    (2, 1, N'B', N'Occupied'),
    (3, 2, N'A', N'Occupied'),
    (4, 2, N'B', N'Available'),
    (5, 3, N'A', N'Occupied'),
    (6, 4, N'A', N'Available'),
    (7, 4, N'B', N'Available'),
    (8, 4, N'C', N'Maintenance');
    SET IDENTITY_INSERT dbo.Beds OFF;
END;

/* ---------------- 3. Cư dân, người liên hệ, liên kết Family ---------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Residents WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.Residents ON;
    INSERT INTO dbo.Residents (Id, Code, FullName, DateOfBirth, Gender, AdmittedAt, DischargedAt, Status, Notes) VALUES
    (1, N'NH-0001', N'Nguyễn Văn An', '1942-03-15', N'Nam', DATEADD(MONTH, -4, @NowUtc), NULL, N'Active', N'Tăng huyết áp (dữ liệu mô phỏng). Thích nghe nhạc trữ tình.'),
    (2, N'NH-0002', N'Trần Thị Bình', '1946-07-02', N'Nữ', DATEADD(MONTH, -3, @NowUtc), NULL, N'Active', N'Đi lại cần hỗ trợ khung tập đi.'),
    (3, N'NH-0003', N'Lê Văn Cường', '1939-11-20', N'Nam', DATEADD(MONTH, -6, @NowUtc), NULL, N'Active', N'Phòng VIP, gia đình thăm cuối tuần.'),
    (4, N'NH-0004', N'Phạm Thị Dung', '1950-01-09', N'Nữ', DATEADD(MONTH, -2, @NowUtc), NULL, N'Active', N'Ăn mềm, hạn chế đường.'),
    (5, N'NH-0005', N'Võ Văn Em', '1944-05-30', N'Nam', DATEADD(MONTH, -5, @NowUtc), DATEADD(DAY, -20, @NowUtc), N'Discharged', N'Đã về sống cùng gia đình.');
    SET IDENTITY_INSERT dbo.Residents OFF;

    INSERT INTO dbo.FamilyContacts (ResidentId, FullName, Relationship, Phone, Email, IsEmergencyContact) VALUES
    (1, N'Nguyễn Thị Bích', N'Con gái', N'0900000008', N'family@nursinghome.local', 1),
    (1, N'Nguyễn Văn Bảo', N'Con trai', N'0911111111', NULL, 0),
    (2, N'Trần Quốc Bảo', N'Con trai', N'0900000009', N'family2@nursinghome.local', 1),
    (3, N'Lê Minh Châu', N'Cháu nội', N'0922222222', NULL, 1),
    (4, N'Phạm Văn Đông', N'Em trai', N'0933333333', NULL, 1);

    INSERT INTO dbo.ResidentFamilyLinks (ResidentId, FamilyAccountId, CanViewHealth, CanViewCare, CanViewMedication, CanViewBilling) VALUES
    (1, 8, 1, 1, 1, 1),
    (2, 9, 1, 1, 0, 1);

    INSERT INTO dbo.BedAssignments (ResidentId, BedId, StartedAt, EndedAt, AssignedById) VALUES
    (1, 1, DATEADD(MONTH, -4, @NowUtc), NULL, 2),
    (2, 2, DATEADD(MONTH, -3, @NowUtc), NULL, 2),
    (3, 5, DATEADD(MONTH, -6, @NowUtc), NULL, 2),
    (4, 3, DATEADD(MONTH, -2, @NowUtc), NULL, 2),
    (5, 4, DATEADD(MONTH, -5, @NowUtc), DATEADD(DAY, -20, @NowUtc), 2);
END;

/* ---------------- 4. Đăng ký gói (giá chốt) ---------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.ResidentSubscriptions WHERE ResidentId = 1)
BEGIN
    INSERT INTO dbo.ResidentSubscriptions (ResidentId, ServicePackageId, StartDate, EndDate, AgreedMonthlyPrice) VALUES
    (1, 2, CAST(DATEADD(HOUR, 7, DATEADD(MONTH, -4, @NowUtc)) AS DATE), NULL, 12000000),
    (2, 1, CAST(DATEADD(HOUR, 7, DATEADD(MONTH, -3, @NowUtc)) AS DATE), NULL, 8000000),
    (3, 3, CAST(DATEADD(HOUR, 7, DATEADD(MONTH, -6, @NowUtc)) AS DATE), NULL, 18000000),
    (4, 2, CAST(DATEADD(HOUR, 7, DATEADD(MONTH, -2, @NowUtc)) AS DATE), NULL, 12000000),
    (5, 1, CAST(DATEADD(HOUR, 7, DATEADD(MONTH, -5, @NowUtc)) AS DATE), CAST(DATEADD(HOUR, 7, DATEADD(DAY, -20, @NowUtc)) AS DATE), 8000000);
END;

/* ---------------- 5. Công việc chăm sóc hôm nay ---------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.CareTasks WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.CareTasks ON;
    INSERT INTO dbo.CareTasks (Id, ResidentId, Title, Instructions, AssignedToId, CreatedById, DueAt, Status) VALUES
    (1, 1, N'Hỗ trợ vệ sinh cá nhân buổi sáng', N'Kiểm tra da vùng lưng, báo điều dưỡng nếu đỏ.', 6, 5, DATEADD(MINUTE, 7 * 60 + 30, @TodayUtc), N'Completed'),
    (2, 1, N'Đo sinh hiệu buổi sáng', N'Đo huyết áp, mạch, nhiệt độ, SpO2.', 6, 5, DATEADD(MINUTE, 8 * 60, @TodayUtc), N'Pending'),
    (3, 2, N'Hỗ trợ đi lại, tập vận động 20 phút', N'Dùng khung tập đi, nghỉ khi mệt.', 6, 5, DATEADD(MINUTE, 9 * 60 + 30, @TodayUtc), N'InProgress'),
    (4, 4, N'Theo dõi bữa trưa (ăn mềm)', N'Ghi lại lượng ăn.', 7, 5, DATEADD(MINUTE, 11 * 60 + 30, @TodayUtc), N'Pending'),
    (5, 3, N'Thay băng vết thương nhỏ ở cẳng chân', N'Theo hướng dẫn của bác sĩ.', 5, 1, DATEADD(MINUTE, 14 * 60, @TodayUtc), N'Pending');
    SET IDENTITY_INSERT dbo.CareTasks OFF;

    INSERT INTO dbo.CareRecords (CareTaskId, Result, Status, PerformedAt, RecordedById, ShareWithFamily) VALUES
    (1, N'Đã hỗ trợ vệ sinh, da bình thường. Ông vui vẻ.', N'Completed', DATEADD(MINUTE, 7 * 60 + 45, @TodayUtc), 6, 1),
    (3, N'Đã tập 10 phút, bà hơi mệt, nghỉ giữa giờ.', N'InProgress', DATEADD(MINUTE, 9 * 60 + 40, @TodayUtc), 6, 1);
END;

/* ---------------- 6. Sinh hiệu (mô phỏng) ---------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.VitalSigns WHERE ResidentId = 1)
BEGIN
    INSERT INTO dbo.VitalSigns (ResidentId, Systolic, Diastolic, HeartRate, Temperature, SpO2, Notes, RecordedAt, RecordedById, ShareWithFamily) VALUES
    (1, 135, 85, 78, 36.6, 97, N'Bình thường.', DATEADD(DAY, -3, DATEADD(HOUR, 1, @TodayUtc)), 5, 1),
    (1, 142, 88, 80, 36.7, 96, NULL, DATEADD(DAY, -2, DATEADD(HOUR, 1, @TodayUtc)), 6, 1),
    (1, 138, 86, 76, 36.5, 98, NULL, DATEADD(DAY, -1, DATEADD(HOUR, 1, @TodayUtc)), 6, 1),
    (2, 120, 75, 72, 36.8, 98, NULL, DATEADD(DAY, -2, DATEADD(HOUR, 1, @TodayUtc)), 5, 1),
    (2, 118, 74, 88, 38.2, 95, N'Sốt nhẹ, đã báo điều dưỡng (mô phỏng).', DATEADD(DAY, -1, DATEADD(HOUR, 2, @TodayUtc)), 6, 1),
    (3, 128, 80, 70, 36.4, 97, NULL, DATEADD(DAY, -1, DATEADD(HOUR, 1, @TodayUtc)), 5, 0),
    (4, 115, 70, 74, 36.6, 99, NULL, DATEADD(DAY, -1, DATEADD(HOUR, 1, @TodayUtc)), 7, 0);
END;

/* ---------------- 7. Lịch thuốc (MÔ PHỎNG, không dùng thực tế) ---------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.MedicationSchedules WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.MedicationSchedules ON;
    INSERT INTO dbo.MedicationSchedules (Id, ResidentId, MedicationName, Dose, Route, StartDate, EndDate, ScheduledTime, ApprovalReference, Instructions, EnteredById, IsActive) VALUES
    (1, 1, N'Thuốc huyết áp (mô phỏng)', N'1 viên', N'Uống', DATEADD(DAY, -30, @Today), NULL, '08:00', N'CĐ-DEMO-001', N'Uống sau ăn sáng.', 4, 1),
    (2, 1, N'Vitamin D3 (mô phỏng)', N'1 viên', N'Uống', DATEADD(DAY, -30, @Today), NULL, '20:00', N'CĐ-DEMO-002', NULL, 4, 1),
    (3, 2, N'Canxi (mô phỏng)', N'1 viên', N'Uống', DATEADD(DAY, -14, @Today), NULL, '08:00', N'CĐ-DEMO-003', N'Uống cùng nhiều nước.', 4, 1),
    (4, 3, N'Thuốc mô phỏng A', N'5 ml', N'Uống', DATEADD(DAY, -7, @Today), DATEADD(DAY, 7, @Today), '12:00', N'CĐ-DEMO-004', NULL, 4, 1),
    (5, 4, N'Thuốc mô phỏng B (đã dừng)', N'1 viên', N'Uống', DATEADD(DAY, -40, @Today), DATEADD(DAY, -10, @Today), '08:00', N'CĐ-DEMO-005', NULL, 4, 0);
    SET IDENTITY_INSERT dbo.MedicationSchedules OFF;

    INSERT INTO dbo.MedicationLogs (MedicationScheduleId, ScheduledAt, RecordedAt, Status, RecordedById, Notes) VALUES
    (1, DATEADD(DAY, -1, DATEADD(HOUR, 8, @TodayUtc)), DATEADD(DAY, -1, DATEADD(MINUTE, 8 * 60 + 10, @TodayUtc)), N'Taken', 6, NULL),
    (2, DATEADD(DAY, -1, DATEADD(HOUR, 20, @TodayUtc)), DATEADD(DAY, -1, DATEADD(MINUTE, 20 * 60 + 5, @TodayUtc)), N'Taken', 5, NULL),
    (3, DATEADD(DAY, -1, DATEADD(HOUR, 8, @TodayUtc)), DATEADD(DAY, -1, DATEADD(MINUTE, 8 * 60 + 20, @TodayUtc)), N'Refused', 6, N'Bà nói buồn nôn, đã báo điều dưỡng.');
END;

/* ---------------- 8. Hóa đơn tháng trước + thanh toán ---------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Invoices WHERE Id = 1)
BEGIN
    DECLARE @PeriodText NVARCHAR(40) = CONCAT(FORMAT(@PrevMonth, 'dd/MM'), N'–', FORMAT(@PrevMonthEnd, 'dd/MM'), N' (', @PrevDays, N' ngày)');
    DECLARE @PeriodCode NVARCHAR(6) = FORMAT(@PrevMonth, 'yyyyMM');
    DECLARE @IssuedAt DATETIME2 = DATEADD(HOUR, -7, CAST(@ThisMonth AS DATETIME2));
    DECLARE @DueDate DATE = DATEADD(DAY, 9, @ThisMonth);

    SET IDENTITY_INSERT dbo.Invoices ON;
    INSERT INTO dbo.Invoices (Id, ResidentId, Number, BillingPeriod, IssuedAt, DueDate, Status) VALUES
    (1, 1, CONCAT(N'INV-', @PeriodCode, N'-00001'), @PrevMonth, @IssuedAt, @DueDate, N'PartiallyPaid'),
    (2, 2, CONCAT(N'INV-', @PeriodCode, N'-00002'), @PrevMonth, @IssuedAt, @DueDate, N'Unpaid'),
    (3, 3, CONCAT(N'INV-', @PeriodCode, N'-00003'), @PrevMonth, @IssuedAt, @DueDate, N'Paid');
    SET IDENTITY_INSERT dbo.Invoices OFF;

    INSERT INTO dbo.InvoiceItems (InvoiceId, Description, Quantity, UnitPrice) VALUES
    (1, CONCAT(N'Gói Tiêu chuẩn: ', @PeriodText), 1, 12000000),
    (1, N'Giặt ủi đồ cá nhân', 2, 150000),
    (2, CONCAT(N'Gói Cơ bản: ', @PeriodText), 1, 8000000),
    (3, CONCAT(N'Gói Cao cấp: ', @PeriodText), 1, 18000000);

    INSERT INTO dbo.Payments (InvoiceId, Amount, Method, PaidAt, Reference, RecordedById) VALUES
    (1, 6000000, N'Cash', DATEADD(DAY, 2, @IssuedAt), N'PT-DEMO-0001', 3),
    (3, 18000000, N'BankTransfer', DATEADD(DAY, 1, @IssuedAt), N'CK-DEMO-0001', 3);
END;

/* ---------------- 9. Thông báo mẫu ---------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Notifications WHERE AccountId IN (8, 9))
BEGIN
    INSERT INTO dbo.Notifications (AccountId, Type, Title, Body, Link, IsRead, CreatedAt) VALUES
    (8, N'Invoice', N'Hóa đơn tháng trước đã phát hành', N'Còn nợ 6.300.000 đ.', N'/family/invoices/1', 0, DATEADD(HOUR, -7, CAST(@ThisMonth AS DATETIME2))),
    (9, N'Invoice', N'Hóa đơn tháng trước đã phát hành', N'Tổng 8.000.000 đ. Có thể thanh toán bằng VietQR.', N'/family/invoices/2', 0, DATEADD(HOUR, -7, CAST(@ThisMonth AS DATETIME2))),
    (5, N'Health', N'Sinh hiệu cần xem lại: Trần Thị Bình', N'Nhiệt độ 38.2°C. Ngưỡng cảnh báo của cơ sở, không phải chẩn đoán.', N'/staff/residents/2', 0, DATEADD(DAY, -1, DATEADD(HOUR, 2, @TodayUtc)));
END;

/* ===============================================================
   10. MODULE VẬN HÀNH & TÀI CHÍNH — BẮT ĐẦU (Viên)
   Dải Id dành cho dữ liệu demo Viên thêm vào BẢNG DÙNG CHUNG (Accounts, Residents, CareTasks...): 100–499.
   Bảng riêng của module thì Id tùy ý. Dùng các biến @Today, @TodayUtc, @NowUtc, @PrevMonth ở đầu file.
   Chèn NGAY TRÊN dòng "KẾT THÚC".
   =============================================================== */

/* ---- 10.1 Tiếp nhận & tham quan (S3-V1): 3 yêu cầu ở 3 bước của phễu + 3 lịch tham quan ---- */
IF NOT EXISTS (SELECT 1 FROM dbo.Inquiries WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.Inquiries ON;
    INSERT INTO dbo.Inquiries (Id, FullName, Phone, Email, ElderName, ElderAge, CareNeeds, InterestedPackageId, Source, Status, AssignedToId, Notes, CreatedAt, UpdatedAt) VALUES
    (1, N'Võ Thanh Tùng', N'0905111222', N'tung.vo@example.com', N'Võ Văn Tư', 82,
        N'Ông đi lại yếu, cần hỗ trợ tắm rửa và nhắc uống thuốc huyết áp.', 2, N'Web', N'New', NULL, NULL,
        DATEADD(HOUR, -3, @NowUtc), NULL),
    (2, N'Lê Thị Ngọc', N'0912333444', NULL, N'Phạm Thị Lan', 76,
        N'Muốn tìm hiểu gói chăm sóc cơ bản, bà còn tự đi lại được.', 1, N'Phone', N'Contacted', 2,
        N'Đã gọi tư vấn, gia đình hẹn đến tham quan.', DATEADD(DAY, -2, @NowUtc), DATEADD(DAY, -1, @NowUtc)),
    (3, N'Huỳnh Quốc Bảo', N'0935555666', N'bao.huynh@example.com', N'Huỳnh Văn Sáu', 88,
        N'Sau tai biến, cần chăm sóc toàn phần và tập vật lý trị liệu.', 3, N'WalkIn', N'Visited', 2,
        N'Đã tham quan phòng đơn tầng 1, gia đình đang cân nhắc.', DATEADD(DAY, -6, @NowUtc), DATEADD(DAY, -3, @NowUtc));
    SET IDENTITY_INSERT dbo.Inquiries OFF;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.TourBookings WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.TourBookings ON;
    INSERT INTO dbo.TourBookings (Id, InquiryId, VisitDate, TimeSlot, GuestCount, Note, Status, ConfirmedById, CreatedAt, UpdatedAt) VALUES
    (1, 1, DATEADD(DAY, 1, @Today), N'Morning', 2, N'Muốn xem phòng 2 giường gần vườn.', N'Requested', NULL, DATEADD(HOUR, -3, @NowUtc), NULL),
    (2, 2, DATEADD(DAY, 2, @Today), N'Afternoon', 3, NULL, N'Confirmed', 2, DATEADD(DAY, -1, @NowUtc), DATEADD(DAY, -1, @NowUtc)),
    (3, 3, DATEADD(DAY, -3, @Today), N'Morning', 2, NULL, N'Done', 2, DATEADD(DAY, -6, @NowUtc), DATEADD(DAY, -3, @NowUtc));
    SET IDENTITY_INSERT dbo.TourBookings OFF;
END;

/* ---- 10.2 Ca trực & phân công phụ trách (S3-V2) ----
   Tuần hiện tại T2–T6: nurse (5) ca sáng, caregiver (6) ca sáng, caregiver2 (7) ca chiều.
   Phụ trách chính: NH-0002 → caregiver (6) ca sáng, NH-0004 → caregiver2 (7) ca chiều
   (chọn cư dân họ đã có việc, để kịch bản "Caregiver hết việc thì hết quyền" với NH-0001 vẫn đúng). */
DECLARE @WeekStart DATE = DATEADD(DAY, -(DATEDIFF(DAY, '19000101', @Today) % 7), @Today); -- thứ Hai (1900-01-01 là thứ Hai, không phụ thuộc DATEFIRST)
IF NOT EXISTS (SELECT 1 FROM dbo.ShiftAssignments WHERE AccountId IN (5, 6, 7))
BEGIN
    INSERT INTO dbo.ShiftAssignments (AccountId, ShiftId, WorkDate, CreatedById, CreatedAt)
    SELECT staff.AccountId, staff.ShiftId, DATEADD(DAY, d.n, @WeekStart), 1, DATEADD(DAY, -3, @NowUtc)
    FROM (VALUES (5, 1), (6, 1), (7, 2)) AS staff (AccountId, ShiftId)
    CROSS JOIN (VALUES (0), (1), (2), (3), (4)) AS d (n);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.ResidentCareAssignments WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.ResidentCareAssignments ON;
    INSERT INTO dbo.ResidentCareAssignments (Id, ResidentId, AccountId, ShiftId, StartDate, EndDate, AssignedById, CreatedAt) VALUES
    (1, 2, 6, 1, DATEADD(DAY, -14, @Today), NULL, 5, DATEADD(DAY, -14, @NowUtc)),
    (2, 4, 7, 2, DATEADD(DAY, -14, @Today), NULL, 5, DATEADD(DAY, -14, @NowUtc));
    SET IDENTITY_INSERT dbo.ResidentCareAssignments OFF;
END;

/* ---- 10.3 Lịch sử cho báo cáo (S3-V3) ----
   Hóa đơn đã trả đủ 2–4 tháng trước (Id 100–104, cùng quy tắc số INV-yyyyMM-<mã cư dân 5 số>) để biểu đồ doanh thu
   không trống, và nhật ký dùng thuốc 7 ngày qua (Taken/Missed/Refused lẫn lộn) cho báo cáo tuân thủ. */
DECLARE @M2 DATE = DATEADD(MONTH, -2, @ThisMonth), @M3 DATE = DATEADD(MONTH, -3, @ThisMonth), @M4 DATE = DATEADD(MONTH, -4, @ThisMonth);
IF NOT EXISTS (SELECT 1 FROM dbo.Invoices WHERE Id = 100)
BEGIN
    SET IDENTITY_INSERT dbo.Invoices ON;
    INSERT INTO dbo.Invoices (Id, ResidentId, Number, BillingPeriod, IssuedAt, DueDate, Status)
    SELECT v.Id, v.ResidentId, CONCAT(N'INV-', FORMAT(v.Period, 'yyyyMM'), N'-', FORMAT(v.ResidentId, '00000')), v.Period,
           DATEADD(HOUR, -7, CAST(DATEADD(MONTH, 1, v.Period) AS DATETIME2)), DATEADD(DAY, 9, DATEADD(MONTH, 1, v.Period)), N'Paid'
    FROM (VALUES (100, 3, @M4), (101, 3, @M3), (102, 3, @M2), (103, 1, @M3), (104, 1, @M2)) AS v (Id, ResidentId, Period);
    SET IDENTITY_INSERT dbo.Invoices OFF;

    INSERT INTO dbo.InvoiceItems (InvoiceId, Description, Quantity, UnitPrice) VALUES
    (100, CONCAT(N'Gói Cao cấp: tháng ', FORMAT(@M4, 'MM/yyyy')), 1, 18000000),
    (101, CONCAT(N'Gói Cao cấp: tháng ', FORMAT(@M3, 'MM/yyyy')), 1, 18000000),
    (102, CONCAT(N'Gói Cao cấp: tháng ', FORMAT(@M2, 'MM/yyyy')), 1, 18000000),
    (103, CONCAT(N'Gói Tiêu chuẩn: tháng ', FORMAT(@M3, 'MM/yyyy')), 1, 12000000),
    (104, CONCAT(N'Gói Tiêu chuẩn: tháng ', FORMAT(@M2, 'MM/yyyy')), 1, 12000000);

    SET IDENTITY_INSERT dbo.Payments ON;
    INSERT INTO dbo.Payments (Id, InvoiceId, Amount, Method, PaidAt, Reference, RecordedById)
    SELECT v.Id, v.InvoiceId, v.Amount, v.Method, DATEADD(DAY, v.AfterDays, i.IssuedAt), CONCAT(N'PT-DEMO-0', v.Id), 3
    FROM (VALUES (100, 100, 18000000, N'BankTransfer', 3), (101, 101, 18000000, N'BankTransfer', 4), (102, 102, 18000000, N'Cash', 2),
                 (103, 103, 12000000, N'Cash', 5), (104, 104, 12000000, N'BankTransfer', 6)) AS v (Id, InvoiceId, Amount, Method, AfterDays)
    JOIN dbo.Invoices AS i ON i.Id = v.InvoiceId;
    SET IDENTITY_INSERT dbo.Payments OFF;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.MedicationLogs WHERE ScheduledAt < DATEADD(DAY, -1, @TodayUtc))
BEGIN
    INSERT INTO dbo.MedicationLogs (MedicationScheduleId, ScheduledAt, RecordedAt, Status, RecordedById, Notes)
    SELECT s.Id, x.ScheduledAt, DATEADD(MINUTE, 15, x.ScheduledAt),
           CASE (d.n + s.Id) % 6 WHEN 0 THEN N'Missed' WHEN 3 THEN N'Refused' ELSE N'Taken' END,
           CASE WHEN s.ResidentId IN (1, 2) THEN 6 ELSE 5 END,
           CASE (d.n + s.Id) % 6 WHEN 0 THEN N'Ngủ quá giờ, đã báo điều dưỡng (mô phỏng).' WHEN 3 THEN N'Từ chối uống (mô phỏng).' END
    FROM dbo.MedicationSchedules AS s
    CROSS JOIN (VALUES (1), (2), (3), (4), (5), (6), (7)) AS d (n)
    CROSS APPLY (SELECT DATEADD(HOUR, -7, DATEADD(MINUTE, DATEDIFF(MINUTE, CAST('00:00' AS TIME), s.ScheduledTime),
                     CAST(DATEADD(DAY, -d.n, @Today) AS DATETIME2))) AS ScheduledAt) AS x
    WHERE s.Id BETWEEN 1 AND 4
      AND DATEADD(DAY, -d.n, @Today) >= s.StartDate
      AND NOT EXISTS (SELECT 1 FROM dbo.MedicationLogs AS l WHERE l.MedicationScheduleId = s.Id AND l.ScheduledAt = x.ScheduledAt);
END;

/* ---- 10.4 Nghỉ phép & bàn giao ca (S3-V4) ----
   1 đơn nghỉ chờ duyệt của caregiver2 (7) tuần sau; bàn giao ca sáng hôm qua của nurse (5) đã được caregiver2 (ca chiều)
   xác nhận, và bàn giao ca sáng hôm nay chưa ai đọc (caregiver2 thấy badge ở "Ca trực"). */
IF NOT EXISTS (SELECT 1 FROM dbo.LeaveRequests WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.LeaveRequests ON;
    INSERT INTO dbo.LeaveRequests (Id, AccountId, Type, FromDate, ToDate, Reason, Status, CreatedAt) VALUES
    (1, 7, N'Leave', DATEADD(DAY, 7, @Today), DATEADD(DAY, 8, @Today), N'Về quê dự đám cưới em gái.', N'Pending', DATEADD(HOUR, -5, @NowUtc));
    SET IDENTITY_INSERT dbo.LeaveRequests OFF;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.ShiftHandovers WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.ShiftHandovers ON;
    INSERT INTO dbo.ShiftHandovers (Id, ShiftId, WorkDate, FromAccountId, Summary, HighlightsJson, AiGenerated, AcknowledgedById, AcknowledgedAt, CreatedAt) VALUES
    (1, 1, DATEADD(DAY, -1, @Today), 5,
        N'Ca sáng ổn định. Bà Trần Thị Bình (NH-0002) sốt nhẹ 38.2°C lúc 09:00, đã chườm mát và báo bác sĩ; ca chiều đo lại nhiệt độ lúc 15:00.',
        N'[{"kind":"Vital","residentId":2,"residentName":"NH-0002 · Trần Thị Bình","text":"Sinh hiệu cần theo dõi lúc 09:00: Nhiệt độ 38.2°C."}]',
        0, 7, DATEADD(HOUR, 7, DATEADD(DAY, -1, @TodayUtc)), DATEADD(HOUR, 7, DATEADD(DAY, -1, @TodayUtc))),
    (2, 1, @Today, 5,
        N'Ông NH-0001 chưa đo sinh hiệu buổi sáng (hộ lý Nam bận hỗ trợ cư dân khác), ca chiều đo giúp trước 15:00. Còn việc thay băng NH-0003 lúc 14:00.',
        N'[{"kind":"Task","residentId":1,"residentName":"NH-0001","text":"Chưa xong: Đo sinh hiệu buổi sáng."},{"kind":"Task","residentId":3,"residentName":"NH-0003","text":"Chưa xong: Thay băng vết thương nhỏ ở cẳng chân."}]',
        0, NULL, NULL, DATEADD(HOUR, 6, @TodayUtc));
    SET IDENTITY_INSERT dbo.ShiftHandovers OFF;
END;

/* ---- 10. MODULE VẬN HÀNH & TÀI CHÍNH — KẾT THÚC ---- */


/* ===============================================================
   11. MODULE CHĂM SÓC & GIA ĐÌNH — BẮT ĐẦU (Huy)
   Dải Id dành cho dữ liệu demo Huy thêm vào BẢNG DÙNG CHUNG: 500–899.
   Chèn NGAY TRÊN dòng "KẾT THÚC".
   =============================================================== */

/* ---- 11. MODULE CHĂM SÓC & GIA ĐÌNH — KẾT THÚC ---- */

COMMIT TRANSACTION;
PRINT N'05_DemoSeed.sql: xong.';
GO
