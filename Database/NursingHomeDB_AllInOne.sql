/* ============================================================
   NursingHomeDB_AllInOne.sql - FILE TU SINH, DUNG SUA TAY
   ------------------------------------------------------------
   Gop 00_CreateDatabase.sql -> 05_DemoSeed.sql thanh mot file de tao
   database NursingHomeDB (schema + view + thu tuc + danh muc + du lieu demo)
   giong het setup-local-db.ps1.

   Sinh lai sau moi lan sua 00..05:  .\Database\build-all-in-one.ps1

   CACH CHAY (chon mot):
     SSMS:   File > Open > file nay, ket noi server, bam Execute (F5).
             Khong can bat SQLCMD Mode. Tab Messages phai ket thuc bang
             "NursingHomeDB_AllInOne.sql: xong." va khong co dong do.
     sqlcmd: sqlcmd -S localhost -E -C -b -I -x -f 65001 -i Database\NursingHomeDB_AllInOne.sql
             (-x: hash BCrypt co dau $, -f 65001: giu tieng Viet)

   Chay lai nhieu lan duoc: bang/du lieu da co thi bo qua, khong nhan ban.
   Muon xoa va tao lai tu dau: bo dau -- o 4 dong "TAO LAI TU DAU" ben duoi
   (MAT HET du lieu trong NursingHomeDB, dung API truoc).

   Tai khoan demo: mat khau chung Demo@123456 (danh sach o phan 05_DemoSeed.sql).
   ============================================================ */
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE master;
GO

-- TAO LAI TU DAU (bo dau -- o 4 dong duoi):
-- IF DB_ID(N'NursingHomeDB') IS NOT NULL
--     ALTER DATABASE [NursingHomeDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
-- IF DB_ID(N'NursingHomeDB') IS NOT NULL DROP DATABASE [NursingHomeDB];
-- GO

-- ============================================================
-- 00_CreateDatabase.sql
-- ============================================================
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
GO

USE [NursingHomeDB];
GO

-- ============================================================
-- 01_Schema.sql
-- ============================================================
SET XACT_ABORT OFF; SET NOCOUNT OFF; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;
GO
/* ============================================================
   NursingHome — 01_Schema.sql  (nguồn sự thật duy nhất của schema)
   ------------------------------------------------------------
   Database-first: API không tạo/migrate DB. Mọi thay đổi bảng/cột/
   ràng buộc phải sửa file này + Entity + NursingHomeContext trong
   cùng một commit (xem Database/README.md).

   Quy ước:
   - Khóa INT IDENTITY; bảng giao dịch/log lớn dùng BIGINT.
   - Enum lưu NVARCHAR + CHECK (EF map bằng HasConversion<string>()).
   - Tiền VND: DECIMAL(18,0). Số lượng: DECIMAL(9,2).
   - Thời điểm: DATETIME2 lưu UTC (SYSUTCDATETIME()); ngày/giờ lịch: DATE/TIME.
     Hiển thị theo giờ cơ sở UTC+7 ở tầng ứng dụng (FacilityClock).
   - Không xóa lịch sử: FK mặc định NO ACTION.

   Idempotent: chỉ tạo bảng/index khi chưa có.
   Chạy: sqlcmd -S localhost -d NursingHomeDB -E -C -b -I -f 65001 -i Database\01_Schema.sql
   ============================================================ */
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- bắt buộc cho filtered index
SET XACT_ABORT ON;
GO

/* ---------------------------------------------------------------
   1. Tài khoản & phiên đăng nhập (Sprint 0)
   --------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Accounts', N'U') IS NULL
CREATE TABLE dbo.Accounts (
    Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Accounts PRIMARY KEY,
    Email         NVARCHAR(256) NOT NULL,
    PasswordHash  NVARCHAR(100) NOT NULL,
    FullName      NVARCHAR(150) NOT NULL,
    Phone         NVARCHAR(30)  NULL,
    Role          NVARCHAR(30)  NOT NULL,
    IsActive      BIT           NOT NULL CONSTRAINT DF_Accounts_IsActive DEFAULT (1),
    AvatarUrl     NVARCHAR(500) NULL,
    CreatedAt     DATETIME2     NOT NULL CONSTRAINT DF_Accounts_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt     DATETIME2     NULL,
    CONSTRAINT UQ_Accounts_Email UNIQUE (Email),
    CONSTRAINT CK_Accounts_Role CHECK (Role IN (N'Admin', N'Receptionist', N'Accountant', N'Doctor', N'Nurse', N'Caregiver', N'Family'))
);
GO

IF OBJECT_ID(N'dbo.RefreshTokens', N'U') IS NULL
CREATE TABLE dbo.RefreshTokens (
    Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY,
    TokenHash           NVARCHAR(64) NOT NULL,
    AccountId           INT          NOT NULL CONSTRAINT FK_RefreshTokens_Accounts REFERENCES dbo.Accounts (Id),
    CreatedAt           DATETIME2    NOT NULL,
    ExpiresAt           DATETIME2    NOT NULL,
    RevokedAt           DATETIME2    NULL,
    ReplacedByTokenHash NVARCHAR(64) NULL,
    CONSTRAINT UQ_RefreshTokens_TokenHash UNIQUE (TokenHash)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RefreshTokens_AccountId')
    CREATE INDEX IX_RefreshTokens_AccountId ON dbo.RefreshTokens (AccountId);
GO

/* ---------------------------------------------------------------
   2. Cư dân, người liên hệ, liên kết Family (Sprint 1)
   --------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Residents', N'U') IS NULL
CREATE TABLE dbo.Residents (
    Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Residents PRIMARY KEY,
    Code          NVARCHAR(30)   NOT NULL,
    FullName      NVARCHAR(150)  NOT NULL,
    DateOfBirth   DATE           NOT NULL,
    Gender        NVARCHAR(20)   NOT NULL,
    PhotoUrl      NVARCHAR(500)  NULL,
    AdmittedAt    DATETIME2      NOT NULL CONSTRAINT DF_Residents_AdmittedAt DEFAULT (SYSUTCDATETIME()),
    DischargedAt  DATETIME2      NULL,
    Status        NVARCHAR(20)   NOT NULL CONSTRAINT DF_Residents_Status DEFAULT (N'Active'),
    Notes         NVARCHAR(2000) NULL,
    CreatedAt     DATETIME2      NOT NULL CONSTRAINT DF_Residents_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt     DATETIME2      NULL,
    CONSTRAINT UQ_Residents_Code UNIQUE (Code),
    CONSTRAINT CK_Residents_Status CHECK (Status IN (N'Active', N'Discharged')),
    CONSTRAINT CK_Residents_Dates CHECK (DischargedAt IS NULL OR DischargedAt >= AdmittedAt)
);
GO

IF OBJECT_ID(N'dbo.FamilyContacts', N'U') IS NULL
CREATE TABLE dbo.FamilyContacts (
    Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FamilyContacts PRIMARY KEY,
    ResidentId         INT           NOT NULL CONSTRAINT FK_FamilyContacts_Residents REFERENCES dbo.Residents (Id),
    FullName           NVARCHAR(150) NOT NULL,
    Relationship       NVARCHAR(50)  NOT NULL,
    Phone              NVARCHAR(30)  NOT NULL,
    Email              NVARCHAR(256) NULL,
    IsEmergencyContact BIT           NOT NULL CONSTRAINT DF_FamilyContacts_Emergency DEFAULT (0),
    CreatedAt          DATETIME2     NOT NULL CONSTRAINT DF_FamilyContacts_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt          DATETIME2     NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FamilyContacts_ResidentId')
    CREATE INDEX IX_FamilyContacts_ResidentId ON dbo.FamilyContacts (ResidentId);
GO

IF OBJECT_ID(N'dbo.ResidentFamilyLinks', N'U') IS NULL
CREATE TABLE dbo.ResidentFamilyLinks (
    Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ResidentFamilyLinks PRIMARY KEY,
    ResidentId        INT       NOT NULL CONSTRAINT FK_ResidentFamilyLinks_Residents REFERENCES dbo.Residents (Id),
    FamilyAccountId   INT       NOT NULL CONSTRAINT FK_ResidentFamilyLinks_Accounts REFERENCES dbo.Accounts (Id),
    CanViewHealth     BIT       NOT NULL CONSTRAINT DF_RFL_Health DEFAULT (0),
    CanViewCare       BIT       NOT NULL CONSTRAINT DF_RFL_Care DEFAULT (0),
    CanViewMedication BIT       NOT NULL CONSTRAINT DF_RFL_Medication DEFAULT (0),
    CanViewBilling    BIT       NOT NULL CONSTRAINT DF_RFL_Billing DEFAULT (0),
    CreatedAt         DATETIME2 NOT NULL CONSTRAINT DF_RFL_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt         DATETIME2 NULL,
    CONSTRAINT UQ_ResidentFamilyLinks UNIQUE (ResidentId, FamilyAccountId)
);
GO

/* ---------------------------------------------------------------
   3. Phòng, giường, phân giường (Sprint 1)
   --------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Rooms', N'U') IS NULL
CREATE TABLE dbo.Rooms (
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Rooms PRIMARY KEY,
    Number      NVARCHAR(30)  NOT NULL,
    Floor       INT           NULL,
    Description NVARCHAR(200) NULL,
    IsActive    BIT           NOT NULL CONSTRAINT DF_Rooms_IsActive DEFAULT (1),
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_Rooms_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt   DATETIME2     NULL,
    CONSTRAINT UQ_Rooms_Number UNIQUE (Number)
);
GO

IF OBJECT_ID(N'dbo.Beds', N'U') IS NULL
CREATE TABLE dbo.Beds (
    Id         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Beds PRIMARY KEY,
    RoomId     INT          NOT NULL CONSTRAINT FK_Beds_Rooms REFERENCES dbo.Rooms (Id),
    Number     NVARCHAR(30) NOT NULL,
    Status     NVARCHAR(20) NOT NULL CONSTRAINT DF_Beds_Status DEFAULT (N'Available'),
    RowVersion ROWVERSION   NOT NULL,
    CreatedAt  DATETIME2    NOT NULL CONSTRAINT DF_Beds_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt  DATETIME2    NULL,
    CONSTRAINT UQ_Beds_Room_Number UNIQUE (RoomId, Number),
    CONSTRAINT CK_Beds_Status CHECK (Status IN (N'Available', N'Occupied', N'Maintenance'))
);
GO

IF OBJECT_ID(N'dbo.BedAssignments', N'U') IS NULL
CREATE TABLE dbo.BedAssignments (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BedAssignments PRIMARY KEY,
    ResidentId   INT       NOT NULL CONSTRAINT FK_BedAssignments_Residents REFERENCES dbo.Residents (Id),
    BedId        INT       NOT NULL CONSTRAINT FK_BedAssignments_Beds REFERENCES dbo.Beds (Id),
    StartedAt    DATETIME2 NOT NULL CONSTRAINT DF_BedAssignments_StartedAt DEFAULT (SYSUTCDATETIME()),
    EndedAt      DATETIME2 NULL,
    AssignedById INT       NOT NULL CONSTRAINT FK_BedAssignments_Accounts REFERENCES dbo.Accounts (Id),
    CreatedAt    DATETIME2 NOT NULL CONSTRAINT DF_BedAssignments_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_BedAssignments_Dates CHECK (EndedAt IS NULL OR EndedAt >= StartedAt)
);
GO
-- Một giường chỉ một cư dân đang ở, một cư dân chỉ một giường đang ở.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_BedAssignments_ActiveBed')
    CREATE UNIQUE INDEX UX_BedAssignments_ActiveBed ON dbo.BedAssignments (BedId) WHERE EndedAt IS NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_BedAssignments_ActiveResident')
    CREATE UNIQUE INDEX UX_BedAssignments_ActiveResident ON dbo.BedAssignments (ResidentId) WHERE EndedAt IS NULL;
GO

/* ---------------------------------------------------------------
   4. Chăm sóc & sức khỏe (Sprint 1)
   --------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.CareTasks', N'U') IS NULL
CREATE TABLE dbo.CareTasks (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CareTasks PRIMARY KEY,
    ResidentId   INT            NOT NULL CONSTRAINT FK_CareTasks_Residents REFERENCES dbo.Residents (Id),
    Title        NVARCHAR(200)  NOT NULL,
    Instructions NVARCHAR(2000) NULL,
    AssignedToId INT            NOT NULL CONSTRAINT FK_CareTasks_AssignedTo REFERENCES dbo.Accounts (Id),
    CreatedById  INT            NOT NULL CONSTRAINT FK_CareTasks_CreatedBy REFERENCES dbo.Accounts (Id),
    DueAt        DATETIME2      NOT NULL,
    Status       NVARCHAR(20)   NOT NULL CONSTRAINT DF_CareTasks_Status DEFAULT (N'Pending'),
    RowVersion   ROWVERSION     NOT NULL,
    CreatedAt    DATETIME2      NOT NULL CONSTRAINT DF_CareTasks_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt    DATETIME2      NULL,
    CONSTRAINT CK_CareTasks_Status CHECK (Status IN (N'Pending', N'InProgress', N'Completed', N'Cancelled'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CareTasks_AssignedTo_DueAt')
    CREATE INDEX IX_CareTasks_AssignedTo_DueAt ON dbo.CareTasks (AssignedToId, DueAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CareTasks_Resident_Status')
    CREATE INDEX IX_CareTasks_Resident_Status ON dbo.CareTasks (ResidentId, Status);
GO

IF OBJECT_ID(N'dbo.CareRecords', N'U') IS NULL
CREATE TABLE dbo.CareRecords (
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CareRecords PRIMARY KEY,
    CareTaskId      INT            NOT NULL CONSTRAINT FK_CareRecords_CareTasks REFERENCES dbo.CareTasks (Id),
    Result          NVARCHAR(2000) NOT NULL,
    Status          NVARCHAR(20)   NOT NULL,
    PerformedAt     DATETIME2      NOT NULL CONSTRAINT DF_CareRecords_PerformedAt DEFAULT (SYSUTCDATETIME()),
    RecordedById    INT            NOT NULL CONSTRAINT FK_CareRecords_Accounts REFERENCES dbo.Accounts (Id),
    ShareWithFamily BIT            NOT NULL CONSTRAINT DF_CareRecords_Share DEFAULT (0),
    CreatedAt       DATETIME2      NOT NULL CONSTRAINT DF_CareRecords_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_CareRecords_Status CHECK (Status IN (N'Pending', N'InProgress', N'Completed', N'Cancelled'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CareRecords_CareTaskId')
    CREATE INDEX IX_CareRecords_CareTaskId ON dbo.CareRecords (CareTaskId);
GO

IF OBJECT_ID(N'dbo.VitalSigns', N'U') IS NULL
CREATE TABLE dbo.VitalSigns (
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_VitalSigns PRIMARY KEY,
    ResidentId      INT            NOT NULL CONSTRAINT FK_VitalSigns_Residents REFERENCES dbo.Residents (Id),
    Systolic        INT            NOT NULL,
    Diastolic       INT            NOT NULL,
    HeartRate       INT            NOT NULL,
    Temperature     DECIMAL(4,1)   NOT NULL,
    SpO2            DECIMAL(5,2)   NULL,
    Notes           NVARCHAR(2000) NULL,
    RecordedAt      DATETIME2      NOT NULL CONSTRAINT DF_VitalSigns_RecordedAt DEFAULT (SYSUTCDATETIME()),
    RecordedById    INT            NOT NULL CONSTRAINT FK_VitalSigns_Accounts REFERENCES dbo.Accounts (Id),
    ShareWithFamily BIT            NOT NULL CONSTRAINT DF_VitalSigns_Share DEFAULT (0),
    CreatedAt       DATETIME2      NOT NULL CONSTRAINT DF_VitalSigns_CreatedAt DEFAULT (SYSUTCDATETIME()),
    -- Chỉ là kiểm tra kỹ thuật, không phải ngưỡng chẩn đoán.
    CONSTRAINT CK_VitalSigns_Range CHECK (
        Systolic BETWEEN 40 AND 300 AND Diastolic BETWEEN 20 AND 200 AND Systolic > Diastolic
        AND HeartRate BETWEEN 20 AND 250 AND Temperature BETWEEN 25 AND 45
        AND (SpO2 IS NULL OR SpO2 BETWEEN 0 AND 100))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VitalSigns_Resident_RecordedAt')
    CREATE INDEX IX_VitalSigns_Resident_RecordedAt ON dbo.VitalSigns (ResidentId, RecordedAt);
GO

/* ---------------------------------------------------------------
   5. Thuốc (Sprint 1)
   --------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.MedicationSchedules', N'U') IS NULL
CREATE TABLE dbo.MedicationSchedules (
    Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MedicationSchedules PRIMARY KEY,
    ResidentId        INT            NOT NULL CONSTRAINT FK_MedicationSchedules_Residents REFERENCES dbo.Residents (Id),
    MedicationName    NVARCHAR(200)  NOT NULL,
    Dose              NVARCHAR(100)  NOT NULL,
    Route             NVARCHAR(100)  NOT NULL,
    StartDate         DATE           NOT NULL,
    EndDate           DATE           NULL,
    ScheduledTime     TIME(0)        NOT NULL,
    ApprovalReference NVARCHAR(200)  NOT NULL,
    Instructions      NVARCHAR(2000) NULL,
    EnteredById       INT            NOT NULL CONSTRAINT FK_MedicationSchedules_Accounts REFERENCES dbo.Accounts (Id),
    IsActive          BIT            NOT NULL CONSTRAINT DF_MedicationSchedules_IsActive DEFAULT (1),
    CreatedAt         DATETIME2      NOT NULL CONSTRAINT DF_MedicationSchedules_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt         DATETIME2      NULL,
    CONSTRAINT CK_MedicationSchedules_Dates CHECK (EndDate IS NULL OR EndDate >= StartDate)
);
GO

IF OBJECT_ID(N'dbo.MedicationLogs', N'U') IS NULL
CREATE TABLE dbo.MedicationLogs (
    Id                   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MedicationLogs PRIMARY KEY,
    MedicationScheduleId INT            NOT NULL CONSTRAINT FK_MedicationLogs_Schedules REFERENCES dbo.MedicationSchedules (Id),
    ScheduledAt          DATETIME2      NOT NULL,
    RecordedAt           DATETIME2      NULL,
    Status               NVARCHAR(20)   NOT NULL,
    RecordedById         INT            NULL CONSTRAINT FK_MedicationLogs_Accounts REFERENCES dbo.Accounts (Id),
    Notes                NVARCHAR(2000) NULL,
    RowVersion           ROWVERSION     NOT NULL,
    CreatedAt            DATETIME2      NOT NULL CONSTRAINT DF_MedicationLogs_CreatedAt DEFAULT (SYSUTCDATETIME()),
    -- Một lịch + một thời điểm chỉ có một bản ghi, không ghi đè lịch sử.
    CONSTRAINT UQ_MedicationLogs_Schedule_Time UNIQUE (MedicationScheduleId, ScheduledAt),
    CONSTRAINT CK_MedicationLogs_Status CHECK (Status IN (N'Scheduled', N'Taken', N'Missed', N'Refused'))
);
GO

/* ---------------------------------------------------------------
   6. Gói dịch vụ, hóa đơn, thanh toán (Sprint 1)
   --------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ServicePackages', N'U') IS NULL
CREATE TABLE dbo.ServicePackages (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ServicePackages PRIMARY KEY,
    Name         NVARCHAR(150)  NOT NULL,
    MonthlyPrice DECIMAL(18,0)  NOT NULL,
    Description  NVARCHAR(2000) NULL,
    IsActive     BIT            NOT NULL CONSTRAINT DF_ServicePackages_IsActive DEFAULT (1),
    CreatedAt    DATETIME2      NOT NULL CONSTRAINT DF_ServicePackages_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt    DATETIME2      NULL,
    CONSTRAINT UQ_ServicePackages_Name UNIQUE (Name),
    CONSTRAINT CK_ServicePackages_Price CHECK (MonthlyPrice >= 0)
);
GO

IF OBJECT_ID(N'dbo.ResidentSubscriptions', N'U') IS NULL
CREATE TABLE dbo.ResidentSubscriptions (
    Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ResidentSubscriptions PRIMARY KEY,
    ResidentId         INT           NOT NULL CONSTRAINT FK_ResidentSubscriptions_Residents REFERENCES dbo.Residents (Id),
    ServicePackageId   INT           NOT NULL CONSTRAINT FK_ResidentSubscriptions_Packages REFERENCES dbo.ServicePackages (Id),
    StartDate          DATE          NOT NULL,
    EndDate            DATE          NULL,
    AgreedMonthlyPrice DECIMAL(18,0) NOT NULL,
    CreatedAt          DATETIME2     NOT NULL CONSTRAINT DF_ResidentSubscriptions_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt          DATETIME2     NULL,
    CONSTRAINT CK_ResidentSubscriptions_Price CHECK (AgreedMonthlyPrice >= 0),
    CONSTRAINT CK_ResidentSubscriptions_Dates CHECK (EndDate IS NULL OR EndDate >= StartDate)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ResidentSubscriptions_ResidentId')
    CREATE INDEX IX_ResidentSubscriptions_ResidentId ON dbo.ResidentSubscriptions (ResidentId, StartDate);
GO

IF OBJECT_ID(N'dbo.Invoices', N'U') IS NULL
CREATE TABLE dbo.Invoices (
    Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Invoices PRIMARY KEY,
    ResidentId    INT          NOT NULL CONSTRAINT FK_Invoices_Residents REFERENCES dbo.Residents (Id),
    Number        NVARCHAR(40) NOT NULL,
    BillingPeriod DATE         NOT NULL,
    IssuedAt      DATETIME2    NULL,
    DueDate       DATE         NOT NULL,
    Status        NVARCHAR(20) NOT NULL CONSTRAINT DF_Invoices_Status DEFAULT (N'Draft'),
    RowVersion    ROWVERSION   NOT NULL,
    CreatedAt     DATETIME2    NOT NULL CONSTRAINT DF_Invoices_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt     DATETIME2    NULL,
    CONSTRAINT UQ_Invoices_Number UNIQUE (Number),
    -- Hóa đơn Cancelled vẫn giữ kỳ để không tạo trùng.
    CONSTRAINT UQ_Invoices_Resident_Period UNIQUE (ResidentId, BillingPeriod),
    CONSTRAINT CK_Invoices_Period CHECK (DAY(BillingPeriod) = 1),
    CONSTRAINT CK_Invoices_DueDate CHECK (DueDate >= BillingPeriod),
    CONSTRAINT CK_Invoices_Status CHECK (Status IN (N'Draft', N'Unpaid', N'PartiallyPaid', N'Paid', N'Cancelled'))
);
GO

IF OBJECT_ID(N'dbo.InvoiceItems', N'U') IS NULL
CREATE TABLE dbo.InvoiceItems (
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InvoiceItems PRIMARY KEY,
    InvoiceId   INT           NOT NULL CONSTRAINT FK_InvoiceItems_Invoices REFERENCES dbo.Invoices (Id),
    Description NVARCHAR(250) NOT NULL,
    Quantity    DECIMAL(9,2)  NOT NULL CONSTRAINT DF_InvoiceItems_Quantity DEFAULT (1),
    UnitPrice   DECIMAL(18,0) NOT NULL,
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_InvoiceItems_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_InvoiceItems_Amount CHECK (Quantity > 0 AND UnitPrice >= 0)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InvoiceItems_InvoiceId')
    CREATE INDEX IX_InvoiceItems_InvoiceId ON dbo.InvoiceItems (InvoiceId);
GO

/* ---------------------------------------------------------------
   7. Thanh toán online PayOS (Sprint 2)
   --------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.PaymentTransactions', N'U') IS NULL
CREATE TABLE dbo.PaymentTransactions (
    Id                BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PaymentTransactions PRIMARY KEY,
    InvoiceId         INT           NOT NULL CONSTRAINT FK_PaymentTransactions_Invoices REFERENCES dbo.Invoices (Id),
    Provider          NVARCHAR(20)  NOT NULL CONSTRAINT DF_PaymentTransactions_Provider DEFAULT (N'PAYOS'),
    Amount            DECIMAL(18,0) NOT NULL,
    Status            NVARCHAR(20)  NOT NULL CONSTRAINT DF_PaymentTransactions_Status DEFAULT (N'Pending'),
    Attempt           INT           NOT NULL CONSTRAINT DF_PaymentTransactions_Attempt DEFAULT (0),
    -- orderCode = Id * 1000 + Attempt (giống StreetBiz): mỗi lần tạo link là một mã mới.
    OrderCode         BIGINT        NULL,
    CheckoutUrl       NVARCHAR(500) NULL,
    QrCode            NVARCHAR(1000) NULL,
    CheckoutPayload   NVARCHAR(MAX) NULL,
    ExpiresAt         DATETIME2     NULL,
    PaidAt            DATETIME2     NULL,
    ProviderReference NVARCHAR(100) NULL,
    CreatedById       INT           NOT NULL CONSTRAINT FK_PaymentTransactions_Accounts REFERENCES dbo.Accounts (Id),
    RowVersion        ROWVERSION    NOT NULL,
    CreatedAt         DATETIME2     NOT NULL CONSTRAINT DF_PaymentTransactions_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt         DATETIME2     NULL,
    CONSTRAINT CK_PaymentTransactions_Amount CHECK (Amount > 0),
    CONSTRAINT CK_PaymentTransactions_Provider CHECK (Provider IN (N'PAYOS')),
    CONSTRAINT CK_PaymentTransactions_Status CHECK (Status IN (N'Pending', N'Paid', N'Expired', N'Cancelled', N'Failed'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_PaymentTransactions_OrderCode')
    CREATE UNIQUE INDEX UX_PaymentTransactions_OrderCode ON dbo.PaymentTransactions (OrderCode) WHERE OrderCode IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PaymentTransactions_Invoice_Status')
    CREATE INDEX IX_PaymentTransactions_Invoice_Status ON dbo.PaymentTransactions (InvoiceId, Status);
GO

IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL
CREATE TABLE dbo.Payments (
    Id                   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payments PRIMARY KEY,
    InvoiceId            INT           NOT NULL CONSTRAINT FK_Payments_Invoices REFERENCES dbo.Invoices (Id),
    Amount               DECIMAL(18,0) NOT NULL,
    Method               NVARCHAR(20)  NOT NULL CONSTRAINT DF_Payments_Method DEFAULT (N'Cash'),
    PaidAt               DATETIME2     NOT NULL CONSTRAINT DF_Payments_PaidAt DEFAULT (SYSUTCDATETIME()),
    -- Mã tham chiếu duy nhất: chặn ghi một giao dịch hai lần (PayOS dùng "PAYOS-{orderCode}").
    Reference            NVARCHAR(100) NOT NULL,
    RecordedById         INT           NULL CONSTRAINT FK_Payments_Accounts REFERENCES dbo.Accounts (Id),
    PaymentTransactionId BIGINT        NULL CONSTRAINT FK_Payments_PaymentTransactions REFERENCES dbo.PaymentTransactions (Id),
    CreatedAt            DATETIME2     NOT NULL CONSTRAINT DF_Payments_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Payments_Reference UNIQUE (Reference),
    CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
    CONSTRAINT CK_Payments_Method CHECK (Method IN (N'Cash', N'BankTransfer', N'PayOS'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payments_InvoiceId')
    CREATE INDEX IX_Payments_InvoiceId ON dbo.Payments (InvoiceId);
GO

IF OBJECT_ID(N'dbo.PaymentWebhookLogs', N'U') IS NULL
CREATE TABLE dbo.PaymentWebhookLogs (
    Id                   BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PaymentWebhookLogs PRIMARY KEY,
    Provider             NVARCHAR(20)  NOT NULL CONSTRAINT DF_PaymentWebhookLogs_Provider DEFAULT (N'PAYOS'),
    Source               NVARCHAR(20)  NOT NULL CONSTRAINT DF_PaymentWebhookLogs_Source DEFAULT (N'Webhook'),
    RawPayload           NVARCHAR(MAX) NOT NULL,
    SignatureValid       BIT           NOT NULL,
    Outcome              NVARCHAR(20)  NOT NULL,
    Reason               NVARCHAR(500) NULL,
    OrderCode            BIGINT        NULL,
    Amount               DECIMAL(18,0) NULL,
    ProviderEventId      NVARCHAR(100) NULL,
    PaymentTransactionId BIGINT        NULL CONSTRAINT FK_PaymentWebhookLogs_Transactions REFERENCES dbo.PaymentTransactions (Id),
    ReceivedAt           DATETIME2     NOT NULL CONSTRAINT DF_PaymentWebhookLogs_ReceivedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_PaymentWebhookLogs_Source CHECK (Source IN (N'Webhook', N'Sync')),
    CONSTRAINT CK_PaymentWebhookLogs_Outcome CHECK (Outcome IN (N'APPLIED', N'DUPLICATE', N'UNMATCHED', N'REJECTED', N'SURPLUS', N'IGNORED'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PaymentWebhookLogs_Outcome')
    CREATE INDEX IX_PaymentWebhookLogs_Outcome ON dbo.PaymentWebhookLogs (Outcome, ReceivedAt);
GO

/* ---------------------------------------------------------------
   8. Thông báo in-app (Sprint 2)
   --------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
CREATE TABLE dbo.Notifications (
    Id        BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY,
    AccountId INT            NOT NULL CONSTRAINT FK_Notifications_Accounts REFERENCES dbo.Accounts (Id),
    Type      NVARCHAR(30)   NOT NULL,
    Title     NVARCHAR(200)  NOT NULL,
    Body      NVARCHAR(1000) NULL,
    Link      NVARCHAR(300)  NULL,
    IsRead    BIT            NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT (0),
    CreatedAt DATETIME2      NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_Notifications_Type CHECK (Type IN (N'Payment', N'Invoice', N'Care', N'Health', N'System'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_Account_IsRead')
    CREATE INDEX IX_Notifications_Account_IsRead ON dbo.Notifications (AccountId, IsRead, CreatedAt DESC);
GO

/* ===============================================================
   9. MODULE VẬN HÀNH & TÀI CHÍNH — BẮT ĐẦU (Viên, Sprint 3+)
   Tiếp nhận/tham quan, ca trực, bàn giao ca, tài chính mở rộng, chat, AI.
   Chỉ Viên thêm bảng trong vùng này; chèn NGAY TRÊN dòng "KẾT THÚC" bên dưới.
   =============================================================== */

/* ---- 9.1 Tiếp nhận & tham quan (S3-V1: UC-17, 18, 21, 22) ---- */
IF OBJECT_ID(N'dbo.Inquiries', N'U') IS NULL
CREATE TABLE dbo.Inquiries (
    Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Inquiries PRIMARY KEY,
    FullName            NVARCHAR(150)  NOT NULL,
    Phone               NVARCHAR(30)   NOT NULL,
    Email               NVARCHAR(256)  NULL,
    ElderName           NVARCHAR(150)  NULL,
    ElderAge            INT            NULL,
    CareNeeds           NVARCHAR(1000) NULL,
    InterestedPackageId INT            NULL CONSTRAINT FK_Inquiries_Packages REFERENCES dbo.ServicePackages (Id),
    Source              NVARCHAR(20)   NOT NULL CONSTRAINT DF_Inquiries_Source DEFAULT (N'Web'),
    Status              NVARCHAR(20)   NOT NULL CONSTRAINT DF_Inquiries_Status DEFAULT (N'New'),
    AssignedToId        INT            NULL CONSTRAINT FK_Inquiries_Accounts REFERENCES dbo.Accounts (Id),
    Notes               NVARCHAR(2000) NULL,
    ResidentId          INT            NULL CONSTRAINT FK_Inquiries_Residents REFERENCES dbo.Residents (Id),
    CreatedAt           DATETIME2      NOT NULL CONSTRAINT DF_Inquiries_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt           DATETIME2      NULL,
    CONSTRAINT CK_Inquiries_Source CHECK (Source IN (N'Web', N'Phone', N'WalkIn', N'Chatbot')),
    CONSTRAINT CK_Inquiries_Status CHECK (Status IN (N'New', N'Contacted', N'Visited', N'Admitted', N'Lost')),
    CONSTRAINT CK_Inquiries_Age CHECK (ElderAge IS NULL OR ElderAge BETWEEN 40 AND 120),
    CONSTRAINT CK_Inquiries_Admitted CHECK (Status <> N'Admitted' OR ResidentId IS NOT NULL)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Inquiries_Status_CreatedAt')
    CREATE INDEX IX_Inquiries_Status_CreatedAt ON dbo.Inquiries (Status, CreatedAt DESC);
GO
IF OBJECT_ID(N'dbo.TourBookings', N'U') IS NULL
CREATE TABLE dbo.TourBookings (
    Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TourBookings PRIMARY KEY,
    InquiryId     INT           NOT NULL CONSTRAINT FK_TourBookings_Inquiries REFERENCES dbo.Inquiries (Id),
    VisitDate     DATE          NOT NULL,                  -- ngày theo giờ cơ sở (UTC+7)
    TimeSlot      NVARCHAR(20)  NOT NULL,
    GuestCount    INT           NOT NULL,
    Note          NVARCHAR(500) NULL,
    Status        NVARCHAR(20)  NOT NULL CONSTRAINT DF_TourBookings_Status DEFAULT (N'Requested'),
    ConfirmedById INT           NULL CONSTRAINT FK_TourBookings_Accounts REFERENCES dbo.Accounts (Id),
    CreatedAt     DATETIME2     NOT NULL CONSTRAINT DF_TourBookings_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt     DATETIME2     NULL,
    CONSTRAINT CK_TourBookings_Slot CHECK (TimeSlot IN (N'Morning', N'Afternoon')),
    CONSTRAINT CK_TourBookings_Guests CHECK (GuestCount BETWEEN 1 AND 10),
    CONSTRAINT CK_TourBookings_Status CHECK (Status IN (N'Requested', N'Confirmed', N'Done', N'Cancelled', N'NoShow'))
);
GO
-- Sức chứa mỗi buổi (Facility:TourSlotCapacity) được kiểm trong transaction Serializable của AdmissionDAO.BookTourAsync;
-- index này giúp khóa phạm vi đúng ngày + buổi thay vì cả bảng.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TourBookings_Date_Slot')
    CREATE INDEX IX_TourBookings_Date_Slot ON dbo.TourBookings (VisitDate, TimeSlot, Status) INCLUDE (GuestCount);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TourBookings_Inquiry')
    CREATE INDEX IX_TourBookings_Inquiry ON dbo.TourBookings (InquiryId);
GO

/* ---- 9.2 Ca trực & phân công phụ trách (S3-V2: UC-44, 45, 46). Danh mục ca ở 04_ReferenceData.sql ---- */
IF OBJECT_ID(N'dbo.Shifts', N'U') IS NULL
CREATE TABLE dbo.Shifts (
    Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Shifts PRIMARY KEY,
    Code      NVARCHAR(20) NOT NULL CONSTRAINT UQ_Shifts_Code UNIQUE,
    Name      NVARCHAR(50) NOT NULL,
    StartTime TIME(0)      NOT NULL,                -- giờ cơ sở (UTC+7)
    EndTime   TIME(0)      NOT NULL,                -- ca đêm: EndTime < StartTime (kết thúc ngày hôm sau)
    IsActive  BIT          NOT NULL CONSTRAINT DF_Shifts_IsActive DEFAULT (1)
);
GO
IF OBJECT_ID(N'dbo.ShiftAssignments', N'U') IS NULL
CREATE TABLE dbo.ShiftAssignments (
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShiftAssignments PRIMARY KEY,
    AccountId   INT           NOT NULL CONSTRAINT FK_ShiftAssignments_Accounts REFERENCES dbo.Accounts (Id),
    ShiftId     INT           NOT NULL CONSTRAINT FK_ShiftAssignments_Shifts REFERENCES dbo.Shifts (Id),
    WorkDate    DATE          NOT NULL,              -- ngày BẮT ĐẦU ca (giờ cơ sở)
    Note        NVARCHAR(500) NULL,
    CreatedById INT           NOT NULL CONSTRAINT FK_ShiftAssignments_CreatedBy REFERENCES dbo.Accounts (Id),
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_ShiftAssignments_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_ShiftAssignments UNIQUE (AccountId, WorkDate, ShiftId)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ShiftAssignments_Date')
    CREATE INDEX IX_ShiftAssignments_Date ON dbo.ShiftAssignments (WorkDate, ShiftId);
GO
IF OBJECT_ID(N'dbo.ResidentCareAssignments', N'U') IS NULL
CREATE TABLE dbo.ResidentCareAssignments (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ResidentCareAssignments PRIMARY KEY,
    ResidentId   INT       NOT NULL CONSTRAINT FK_RCA_Residents REFERENCES dbo.Residents (Id),
    AccountId    INT       NOT NULL CONSTRAINT FK_RCA_Accounts REFERENCES dbo.Accounts (Id),
    ShiftId      INT       NULL CONSTRAINT FK_RCA_Shifts REFERENCES dbo.Shifts (Id),   -- NULL = mọi ca
    StartDate    DATE      NOT NULL,
    EndDate      DATE      NULL,                                                      -- NULL = đang hiệu lực
    AssignedById INT       NOT NULL CONSTRAINT FK_RCA_AssignedBy REFERENCES dbo.Accounts (Id),
    CreatedAt    DATETIME2 NOT NULL CONSTRAINT DF_RCA_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_RCA_Dates CHECK (EndDate IS NULL OR EndDate >= StartDate)
);
GO
-- Một người phụ trách chính đang hiệu lực cho mỗi (cư dân, ca). Các NULL coi là bằng nhau trong unique index,
-- nên mỗi cư dân chỉ một phân công "mọi ca" đang hiệu lực. "Mọi ca" + ca cụ thể do StaffingDAO kiểm thêm.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_RCA_ActivePrimary')
    CREATE UNIQUE INDEX UX_RCA_ActivePrimary ON dbo.ResidentCareAssignments (ResidentId, ShiftId) WHERE EndDate IS NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RCA_Account_Active')
    CREATE INDEX IX_RCA_Account_Active ON dbo.ResidentCareAssignments (AccountId, ResidentId) WHERE EndDate IS NULL;
GO

/* ---- 9.3 Nghỉ phép/đổi ca & bàn giao ca (S3-V4: UC-47, 49) ---- */
-- Đổi ca: ShiftAssignmentId = ca của người xin, SwapShiftAssignmentId = ca của đồng nghiệp. Duyệt = hoán AccountId
-- của 2 dòng ShiftAssignments trong một transaction (StaffingDAO.ApproveSwapAsync).
-- Khi một ca bị bỏ (Admin bỏ ca, duyệt nghỉ phép), StaffingDAO hủy các đơn đổi ca đang chờ và gỡ tham chiếu tới ca đó,
-- nên CK_LeaveRequests_Swap chỉ bắt buộc đủ 2 ca khi đơn còn Pending.
IF OBJECT_ID(N'dbo.LeaveRequests', N'U') IS NULL
CREATE TABLE dbo.LeaveRequests (
    Id                    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LeaveRequests PRIMARY KEY,
    AccountId             INT           NOT NULL CONSTRAINT FK_LeaveRequests_Accounts REFERENCES dbo.Accounts (Id),
    Type                  NVARCHAR(10)  NOT NULL,
    FromDate              DATE          NOT NULL,                      -- đổi ca: ngày của 2 ca (nhỏ → lớn)
    ToDate                DATE          NOT NULL,
    ShiftAssignmentId     INT           NULL CONSTRAINT FK_LeaveRequests_MyShift REFERENCES dbo.ShiftAssignments (Id),
    SwapWithAccountId     INT           NULL CONSTRAINT FK_LeaveRequests_SwapWith REFERENCES dbo.Accounts (Id),
    SwapShiftAssignmentId INT           NULL CONSTRAINT FK_LeaveRequests_TheirShift REFERENCES dbo.ShiftAssignments (Id),
    Reason                NVARCHAR(500) NOT NULL,
    Status                NVARCHAR(20)  NOT NULL CONSTRAINT DF_LeaveRequests_Status DEFAULT (N'Pending'),
    ReviewedById          INT           NULL CONSTRAINT FK_LeaveRequests_Reviewer REFERENCES dbo.Accounts (Id),
    ReviewedAt            DATETIME2     NULL,
    ReviewNote            NVARCHAR(500) NULL,
    CreatedAt             DATETIME2     NOT NULL CONSTRAINT DF_LeaveRequests_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_LeaveRequests_Type CHECK (Type IN (N'Leave', N'Swap')),
    CONSTRAINT CK_LeaveRequests_Status CHECK (Status IN (N'Pending', N'Approved', N'Rejected', N'Cancelled')),
    CONSTRAINT CK_LeaveRequests_Dates CHECK (ToDate >= FromDate),
    CONSTRAINT CK_LeaveRequests_Swap CHECK (Type = N'Leave' OR Status <> N'Pending'
        OR (ShiftAssignmentId IS NOT NULL AND SwapShiftAssignmentId IS NOT NULL AND SwapWithAccountId IS NOT NULL))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LeaveRequests_Account_Status')
    CREATE INDEX IX_LeaveRequests_Account_Status ON dbo.LeaveRequests (AccountId, Status);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LeaveRequests_Status_CreatedAt')
    CREATE INDEX IX_LeaveRequests_Status_CreatedAt ON dbo.LeaveRequests (Status, CreatedAt DESC);
GO
IF OBJECT_ID(N'dbo.ShiftHandovers', N'U') IS NULL
CREATE TABLE dbo.ShiftHandovers (
    Id               BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShiftHandovers PRIMARY KEY,
    ShiftId          INT            NOT NULL CONSTRAINT FK_ShiftHandovers_Shifts REFERENCES dbo.Shifts (Id),
    WorkDate         DATE           NOT NULL,                          -- ngày BẮT ĐẦU ca (giờ cơ sở)
    FromAccountId    INT            NOT NULL CONSTRAINT FK_ShiftHandovers_From REFERENCES dbo.Accounts (Id),
    Summary          NVARCHAR(4000) NOT NULL,
    HighlightsJson   NVARCHAR(MAX)  NULL,                              -- các mục gợi ý đã chọn (JSON), S4-V2 dùng lại
    AiGenerated      BIT            NOT NULL CONSTRAINT DF_ShiftHandovers_Ai DEFAULT (0),
    AcknowledgedById INT            NULL CONSTRAINT FK_ShiftHandovers_Ack REFERENCES dbo.Accounts (Id),
    AcknowledgedAt   DATETIME2      NULL,
    CreatedAt        DATETIME2      NOT NULL CONSTRAINT DF_ShiftHandovers_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_ShiftHandovers UNIQUE (WorkDate, ShiftId, FromAccountId),
    CONSTRAINT CK_ShiftHandovers_Ack CHECK ((AcknowledgedById IS NULL AND AcknowledgedAt IS NULL)
        OR (AcknowledgedById IS NOT NULL AND AcknowledgedAt IS NOT NULL))
);
GO

/* ---- 9.4 Dịch vụ phát sinh, nhắc nợ (S3-V5: UC-85, 86, 88, 97, 98, 118). Danh mục mẫu ở 04_ReferenceData.sql ---- */
IF OBJECT_ID(N'dbo.ExtraServices', N'U') IS NULL
CREATE TABLE dbo.ExtraServices (
    Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ExtraServices PRIMARY KEY,
    Name      NVARCHAR(150) NOT NULL CONSTRAINT UQ_ExtraServices_Name UNIQUE,
    Unit      NVARCHAR(30)  NOT NULL,
    UnitPrice DECIMAL(18,0) NOT NULL,
    IsActive  BIT           NOT NULL CONSTRAINT DF_ExtraServices_IsActive DEFAULT (1),
    CreatedAt DATETIME2     NOT NULL CONSTRAINT DF_ExtraServices_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt DATETIME2     NULL,
    CONSTRAINT CK_ExtraServices_Price CHECK (UnitPrice >= 0)
);
GO
-- Một lần sử dụng: đơn giá chốt lúc ghi. InvoiceItemId gán khi tạo hóa đơn (BillingDAO.CreateInvoiceAsync, cùng transaction);
-- đã có InvoiceItemId thì không sửa/xóa. Hủy hóa đơn → gỡ InvoiceItemId để vào hóa đơn kỳ sau.
IF OBJECT_ID(N'dbo.ServiceUsages', N'U') IS NULL
CREATE TABLE dbo.ServiceUsages (
    Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ServiceUsages PRIMARY KEY,
    ResidentId     INT           NOT NULL CONSTRAINT FK_ServiceUsages_Residents REFERENCES dbo.Residents (Id),
    ExtraServiceId INT           NOT NULL CONSTRAINT FK_ServiceUsages_Services REFERENCES dbo.ExtraServices (Id),
    UsedOn         DATE          NOT NULL,                              -- giờ cơ sở
    Quantity       DECIMAL(9,2)  NOT NULL,
    UnitPrice      DECIMAL(18,0) NOT NULL,                              -- giá chốt lúc ghi
    Note           NVARCHAR(500) NULL,
    RecordedById   INT           NOT NULL CONSTRAINT FK_ServiceUsages_Accounts REFERENCES dbo.Accounts (Id),
    InvoiceItemId  INT           NULL CONSTRAINT FK_ServiceUsages_InvoiceItems REFERENCES dbo.InvoiceItems (Id),
    CreatedAt      DATETIME2     NOT NULL CONSTRAINT DF_ServiceUsages_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_ServiceUsages_Quantity CHECK (Quantity > 0),
    CONSTRAINT CK_ServiceUsages_Price CHECK (UnitPrice >= 0)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ServiceUsages_Resident_Uninvoiced')
    CREATE INDEX IX_ServiceUsages_Resident_Uninvoiced ON dbo.ServiceUsages (ResidentId, UsedOn) WHERE InvoiceItemId IS NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ServiceUsages_InvoiceItem')
    CREATE UNIQUE INDEX UX_ServiceUsages_InvoiceItem ON dbo.ServiceUsages (InvoiceItemId) WHERE InvoiceItemId IS NOT NULL;
GO
IF OBJECT_ID(N'dbo.InvoiceReminders', N'U') IS NULL
CREATE TABLE dbo.InvoiceReminders (
    Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InvoiceReminders PRIMARY KEY,
    InvoiceId INT          NOT NULL CONSTRAINT FK_InvoiceReminders_Invoices REFERENCES dbo.Invoices (Id),
    Stage     NVARCHAR(20) NOT NULL,
    SentAt    DATETIME2    NOT NULL CONSTRAINT DF_InvoiceReminders_SentAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_InvoiceReminders_Stage CHECK (Stage IN (N'Before3Days', N'Overdue', N'Overdue7')),
    CONSTRAINT UQ_InvoiceReminders UNIQUE (InvoiceId, Stage)    -- mỗi mốc chỉ nhắc một lần (job ghi dòng TRƯỚC rồi mới gửi)
);
GO

/* ---- 9.5 Chat gia đình – nhân viên (S3-V6: UC-106) ---- */
-- Mỗi (cư dân, tài khoản gia đình) một cuộc trò chuyện; mọi nhân viên được phép cùng trả lời trong đó.
IF OBJECT_ID(N'dbo.Conversations', N'U') IS NULL
CREATE TABLE dbo.Conversations (
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Conversations PRIMARY KEY,
    ResidentId      INT       NOT NULL CONSTRAINT FK_Conversations_Residents REFERENCES dbo.Residents (Id),
    FamilyAccountId INT       NOT NULL CONSTRAINT FK_Conversations_Accounts REFERENCES dbo.Accounts (Id),
    CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_Conversations_CreatedAt DEFAULT (SYSUTCDATETIME()),
    LastMessageAt   DATETIME2 NULL,
    CONSTRAINT UQ_Conversations UNIQUE (ResidentId, FamilyAccountId)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Conversations_LastMessageAt')
    CREATE INDEX IX_Conversations_LastMessageAt ON dbo.Conversations (LastMessageAt DESC) INCLUDE (ResidentId, FamilyAccountId);
GO
IF OBJECT_ID(N'dbo.Messages', N'U') IS NULL
CREATE TABLE dbo.Messages (
    Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Messages PRIMARY KEY,
    ConversationId INT            NOT NULL CONSTRAINT FK_Messages_Conversations REFERENCES dbo.Conversations (Id),
    SenderId       INT            NOT NULL CONSTRAINT FK_Messages_Accounts REFERENCES dbo.Accounts (Id),
    FromFamily     BIT            NOT NULL,
    Body           NVARCHAR(2000) NOT NULL,
    SentAt         DATETIME2      NOT NULL CONSTRAINT DF_Messages_SentAt DEFAULT (SYSUTCDATETIME()),
    ReadAt         DATETIME2      NULL,                                 -- bên kia đã đọc
    CONSTRAINT CK_Messages_Body CHECK (LEN(Body) > 0)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Messages_Conversation_SentAt')
    CREATE INDEX IX_Messages_Conversation_SentAt ON dbo.Messages (ConversationId, SentAt DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Messages_Unread')
    CREATE INDEX IX_Messages_Unread ON dbo.Messages (ConversationId, FromFamily) WHERE ReadAt IS NULL;
GO

/* ---- 9.6 Hạ tầng AI dùng chung (S4-V1: AI-01, UC-20). Huy dùng lại cho trợ lý gia đình (S4-H1) ---- */
-- Hội thoại AI: Public gắn AnonymousKey (UUID lưu localStorage của khách), Family/Staff gắn AccountId.
IF OBJECT_ID(N'dbo.AiConversations', N'U') IS NULL
CREATE TABLE dbo.AiConversations (
    Id            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AiConversations PRIMARY KEY,
    Channel       NVARCHAR(10)  NOT NULL,
    AccountId     INT           NULL CONSTRAINT FK_AiConversations_Accounts REFERENCES dbo.Accounts (Id),
    AnonymousKey  NVARCHAR(64)  NULL,
    Title         NVARCHAR(200) NULL,
    CreatedAt     DATETIME2     NOT NULL CONSTRAINT DF_AiConversations_CreatedAt DEFAULT (SYSUTCDATETIME()),
    LastMessageAt DATETIME2     NULL,
    CONSTRAINT CK_AiConversations_Channel CHECK (Channel IN (N'Public', N'Family', N'Staff')),
    CONSTRAINT CK_AiConversations_Owner CHECK (AccountId IS NOT NULL OR AnonymousKey IS NOT NULL)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AiConversations_AnonymousKey')
    CREATE UNIQUE INDEX UX_AiConversations_AnonymousKey ON dbo.AiConversations (AnonymousKey) WHERE AnonymousKey IS NOT NULL;
GO
-- Mỗi tài khoản một hội thoại cho mỗi kênh (Family, Staff).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AiConversations_Account_Channel')
    CREATE UNIQUE INDEX UX_AiConversations_Account_Channel ON dbo.AiConversations (AccountId, Channel) WHERE AccountId IS NOT NULL;
GO
IF OBJECT_ID(N'dbo.AiMessages', N'U') IS NULL
CREATE TABLE dbo.AiMessages (
    Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AiMessages PRIMARY KEY,
    ConversationId BIGINT        NOT NULL CONSTRAINT FK_AiMessages_Conversations REFERENCES dbo.AiConversations (Id),
    Role           NVARCHAR(10)  NOT NULL,                              -- user | assistant | tool (tool: kết quả JSON, để kiểm tra lại)
    Content        NVARCHAR(MAX) NOT NULL,
    ToolName       NVARCHAR(100) NULL,
    InputTokens    INT           NULL,
    OutputTokens   INT           NULL,
    CreatedAt      DATETIME2     NOT NULL CONSTRAINT DF_AiMessages_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_AiMessages_Role CHECK (Role IN (N'user', N'assistant', N'tool'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AiMessages_Conversation')
    CREATE INDEX IX_AiMessages_Conversation ON dbo.AiMessages (ConversationId, Id);
GO
-- Tài liệu tri thức cho tool search_knowledge. IsPublic = 0: chỉ trợ lý nội bộ/gia đình được dùng.
IF OBJECT_ID(N'dbo.AiKnowledgeDocs', N'U') IS NULL
CREATE TABLE dbo.AiKnowledgeDocs (
    Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AiKnowledgeDocs PRIMARY KEY,
    Title     NVARCHAR(200) NOT NULL,
    Content   NVARCHAR(MAX) NOT NULL,
    Tags      NVARCHAR(200) NULL,
    IsPublic  BIT           NOT NULL CONSTRAINT DF_AiKnowledgeDocs_IsPublic DEFAULT (1),
    IsActive  BIT           NOT NULL CONSTRAINT DF_AiKnowledgeDocs_IsActive DEFAULT (1),
    UpdatedAt DATETIME2     NOT NULL CONSTRAINT DF_AiKnowledgeDocs_UpdatedAt DEFAULT (SYSUTCDATETIME())
);
GO

/* ---- 9.7 Tài khoản & ảnh (BL-V1: UC-07, UC-24 dùng cột có sẵn; UC-08 mã mời tài khoản gia đình) ----
   Chỉ lưu SHA-256 của mã mời (hex in hoa). Mã dùng một lần: UsedAt + AccountId đi cùng nhau; đã dùng thì không thu hồi. */
IF OBJECT_ID(N'dbo.FamilyInvitations', N'U') IS NULL
CREATE TABLE dbo.FamilyInvitations (
    Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FamilyInvitations PRIMARY KEY,
    CodeHash          CHAR(64)      NOT NULL CONSTRAINT UQ_FamilyInvitations_CodeHash UNIQUE,
    Email             NVARCHAR(256) NOT NULL,
    ResidentId        INT           NOT NULL CONSTRAINT FK_FamilyInvitations_Residents REFERENCES dbo.Residents (Id),
    Relationship      NVARCHAR(50)  NOT NULL,
    CanViewHealth     BIT           NOT NULL CONSTRAINT DF_FamilyInvitations_Health DEFAULT (1),
    CanViewCare       BIT           NOT NULL CONSTRAINT DF_FamilyInvitations_Care DEFAULT (1),
    CanViewMedication BIT           NOT NULL CONSTRAINT DF_FamilyInvitations_Medication DEFAULT (0),
    CanViewBilling    BIT           NOT NULL CONSTRAINT DF_FamilyInvitations_Billing DEFAULT (1),
    CreatedById       INT           NOT NULL CONSTRAINT FK_FamilyInvitations_Creator REFERENCES dbo.Accounts (Id),
    CreatedAt         DATETIME2     NOT NULL CONSTRAINT DF_FamilyInvitations_CreatedAt DEFAULT (SYSUTCDATETIME()),
    ExpiresAt         DATETIME2     NOT NULL,
    UsedAt            DATETIME2     NULL,
    AccountId         INT           NULL CONSTRAINT FK_FamilyInvitations_Account REFERENCES dbo.Accounts (Id),
    RevokedAt         DATETIME2     NULL,
    CONSTRAINT CK_FamilyInvitations_Used CHECK ((UsedAt IS NULL AND AccountId IS NULL) OR (UsedAt IS NOT NULL AND AccountId IS NOT NULL)),
    CONSTRAINT CK_FamilyInvitations_UsedOrRevoked CHECK (UsedAt IS NULL OR RevokedAt IS NULL)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FamilyInvitations_Resident' AND object_id = OBJECT_ID(N'dbo.FamilyInvitations'))
CREATE INDEX IX_FamilyInvitations_Resident ON dbo.FamilyInvitations (ResidentId, CreatedAt DESC);
GO

/* ---- 9.8 Hồ sơ lưu trú (BL-V2: UC-29 giấy tờ, UC-30 hợp đồng, UC-31 tạm vắng, UC-33 tái nhập viện, UC-99 tiền cọc) ----
   File giấy tờ/hợp đồng lưu RIÊNG TƯ trên máy chạy API (App_Data/documents), chỉ tải qua endpoint có kiểm quyền. */
IF OBJECT_ID(N'dbo.ResidentDocuments', N'U') IS NULL
CREATE TABLE dbo.ResidentDocuments (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ResidentDocuments PRIMARY KEY,
    ResidentId   INT            NOT NULL CONSTRAINT FK_ResidentDocuments_Residents REFERENCES dbo.Residents (Id),
    Kind         NVARCHAR(20)   NOT NULL,
    Title        NVARCHAR(200)  NOT NULL,
    FileName     NVARCHAR(260)  NOT NULL,
    StoredName   VARCHAR(80)    NOT NULL CONSTRAINT UQ_ResidentDocuments_Stored UNIQUE,
    ContentType  VARCHAR(100)   NOT NULL,
    SizeBytes    BIGINT         NOT NULL,
    UploadedById INT            NOT NULL CONSTRAINT FK_ResidentDocuments_Uploader REFERENCES dbo.Accounts (Id),
    CreatedAt    DATETIME2      NOT NULL CONSTRAINT DF_ResidentDocuments_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_ResidentDocuments_Kind CHECK (Kind IN (N'IdCard', N'Insurance', N'Medical', N'Other')),
    CONSTRAINT CK_ResidentDocuments_Size CHECK (SizeBytes > 0)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ResidentDocuments_Resident' AND object_id = OBJECT_ID(N'dbo.ResidentDocuments'))
CREATE INDEX IX_ResidentDocuments_Resident ON dbo.ResidentDocuments (ResidentId, CreatedAt DESC);
GO

IF OBJECT_ID(N'dbo.ResidentContracts', N'U') IS NULL
CREATE TABLE dbo.ResidentContracts (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ResidentContracts PRIMARY KEY,
    ResidentId   INT            NOT NULL CONSTRAINT FK_ResidentContracts_Residents REFERENCES dbo.Residents (Id),
    Number       NVARCHAR(30)   NOT NULL CONSTRAINT UQ_ResidentContracts_Number UNIQUE,
    SignedOn     DATE           NOT NULL,
    StartDate    DATE           NOT NULL,
    EndDate      DATE           NULL,
    MonthlyPrice DECIMAL(18,0)  NULL,
    Notes        NVARCHAR(500)  NULL,
    FileName     NVARCHAR(260)  NULL,
    StoredName   VARCHAR(80)    NULL,
    ContentType  VARCHAR(100)   NULL,
    SizeBytes    BIGINT         NULL,
    Status       NVARCHAR(20)   NOT NULL CONSTRAINT DF_ResidentContracts_Status DEFAULT (N'Active'),
    CreatedById  INT            NOT NULL CONSTRAINT FK_ResidentContracts_Creator REFERENCES dbo.Accounts (Id),
    CreatedAt    DATETIME2      NOT NULL CONSTRAINT DF_ResidentContracts_CreatedAt DEFAULT (SYSUTCDATETIME()),
    EndedAt      DATETIME2      NULL,
    CONSTRAINT CK_ResidentContracts_Status CHECK (Status IN (N'Active', N'Ended')),
    CONSTRAINT CK_ResidentContracts_Dates CHECK (EndDate IS NULL OR EndDate >= StartDate),
    CONSTRAINT CK_ResidentContracts_Price CHECK (MonthlyPrice IS NULL OR MonthlyPrice >= 0),
    CONSTRAINT CK_ResidentContracts_File CHECK ((StoredName IS NULL AND FileName IS NULL) OR (StoredName IS NOT NULL AND FileName IS NOT NULL))
);
GO

-- Mỗi cư dân tối đa một hợp đồng đang hiệu lực.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ResidentContracts_Active' AND object_id = OBJECT_ID(N'dbo.ResidentContracts'))
CREATE UNIQUE INDEX UX_ResidentContracts_Active ON dbo.ResidentContracts (ResidentId) WHERE Status = N'Active';
GO

IF OBJECT_ID(N'dbo.ResidentLeaves', N'U') IS NULL
CREATE TABLE dbo.ResidentLeaves (
    Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ResidentLeaves PRIMARY KEY,
    ResidentId       INT           NOT NULL CONSTRAINT FK_ResidentLeaves_Residents REFERENCES dbo.Residents (Id),
    Kind             NVARCHAR(20)  NOT NULL,
    StartAt          DATETIME2     NOT NULL,
    ExpectedReturnOn DATE          NULL,
    ReturnedAt       DATETIME2     NULL,
    Reason           NVARCHAR(500) NOT NULL,
    CreatedById      INT           NOT NULL CONSTRAINT FK_ResidentLeaves_Creator REFERENCES dbo.Accounts (Id),
    ReturnedById     INT           NULL CONSTRAINT FK_ResidentLeaves_Returner REFERENCES dbo.Accounts (Id),
    CreatedAt        DATETIME2     NOT NULL CONSTRAINT DF_ResidentLeaves_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_ResidentLeaves_Kind CHECK (Kind IN (N'Home', N'Hospital', N'Other')),
    CONSTRAINT CK_ResidentLeaves_Return CHECK (ReturnedAt IS NULL OR ReturnedAt >= StartAt)
);
GO

-- Mỗi cư dân tối đa một lần tạm vắng chưa về.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ResidentLeaves_Open' AND object_id = OBJECT_ID(N'dbo.ResidentLeaves'))
CREATE UNIQUE INDEX UX_ResidentLeaves_Open ON dbo.ResidentLeaves (ResidentId) WHERE ReturnedAt IS NULL;
GO

-- Lần lưu trú đã kết thúc, ghi lại khi tái nhập viện (Residents.AdmittedAt/DischargedAt chỉ giữ lần hiện tại).
IF OBJECT_ID(N'dbo.ResidentStays', N'U') IS NULL
CREATE TABLE dbo.ResidentStays (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ResidentStays PRIMARY KEY,
    ResidentId   INT           NOT NULL CONSTRAINT FK_ResidentStays_Residents REFERENCES dbo.Residents (Id),
    AdmittedAt   DATETIME2     NOT NULL,
    DischargedAt DATETIME2     NOT NULL,
    RecordedById INT           NOT NULL CONSTRAINT FK_ResidentStays_Recorder REFERENCES dbo.Accounts (Id),
    RecordedAt   DATETIME2     NOT NULL CONSTRAINT DF_ResidentStays_RecordedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_ResidentStays_Dates CHECK (DischargedAt >= AdmittedAt)
);
GO

IF OBJECT_ID(N'dbo.Deposits', N'U') IS NULL
CREATE TABLE dbo.Deposits (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Deposits PRIMARY KEY,
    ResidentId   INT           NOT NULL CONSTRAINT FK_Deposits_Residents REFERENCES dbo.Residents (Id),
    Amount       DECIMAL(18,0) NOT NULL,
    Method       NVARCHAR(20)  NOT NULL,
    Reference    NVARCHAR(100) NULL,
    ReceivedAt   DATETIME2     NOT NULL,
    ReceivedById INT           NOT NULL CONSTRAINT FK_Deposits_Receiver REFERENCES dbo.Accounts (Id),
    RefundedAt   DATETIME2     NULL,
    RefundAmount DECIMAL(18,0) NULL,
    RefundNote   NVARCHAR(500) NULL,
    RefundedById INT           NULL CONSTRAINT FK_Deposits_Refunder REFERENCES dbo.Accounts (Id),
    CONSTRAINT CK_Deposits_Amount CHECK (Amount > 0),
    CONSTRAINT CK_Deposits_Method CHECK (Method IN (N'Cash', N'BankTransfer')),
    CONSTRAINT CK_Deposits_Refund CHECK ((RefundedAt IS NULL AND RefundAmount IS NULL AND RefundedById IS NULL)
        OR (RefundedAt IS NOT NULL AND RefundAmount IS NOT NULL AND RefundedById IS NOT NULL AND RefundAmount >= 0 AND RefundAmount <= Amount))
);
GO

-- Mỗi cư dân tối đa một khoản cọc đang giữ.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Deposits_Held' AND object_id = OBJECT_ID(N'dbo.Deposits'))
CREATE UNIQUE INDEX UX_Deposits_Held ON dbo.Deposits (ResidentId) WHERE RefundedAt IS NULL;
GO

/* ---- 9.9 Phòng & giường mở rộng (BL-V3: UC-36 loại phòng & phụ thu, UC-41 giữ giường, UC-42 báo hỏng & sửa chữa) ----
   Rooms.RoomTypeId thêm bằng ALTER có kiểm tra (bảng lõi mục 3 tạo trước RoomTypes) → chạy được cho cả DB mới và DB đang có dữ liệu. */
IF OBJECT_ID(N'dbo.RoomTypes', N'U') IS NULL
CREATE TABLE dbo.RoomTypes (
    Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RoomTypes PRIMARY KEY,
    Name           NVARCHAR(100) NOT NULL CONSTRAINT UQ_RoomTypes_Name UNIQUE,
    DailySurcharge DECIMAL(18,0) NOT NULL CONSTRAINT DF_RoomTypes_Surcharge DEFAULT (0),
    Description    NVARCHAR(300) NULL,
    IsActive       BIT           NOT NULL CONSTRAINT DF_RoomTypes_IsActive DEFAULT (1),
    CreatedAt      DATETIME2     NOT NULL CONSTRAINT DF_RoomTypes_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt      DATETIME2     NULL,
    CONSTRAINT CK_RoomTypes_Surcharge CHECK (DailySurcharge >= 0)
);
GO

IF COL_LENGTH(N'dbo.Rooms', N'RoomTypeId') IS NULL
    ALTER TABLE dbo.Rooms ADD RoomTypeId INT NULL CONSTRAINT FK_Rooms_RoomTypes REFERENCES dbo.RoomTypes (Id);
GO

-- Giữ giường cho khách sắp nhập viện. Status: Active (đang giữ) | Converted (đã xếp đúng người) | Released (hủy/hết hạn).
-- Active quá ExpiresAt được coi là hết hạn ngay khi đọc; lần xếp/giữ kế tiếp tự chuyển thành Released.
IF OBJECT_ID(N'dbo.BedReservations', N'U') IS NULL
CREATE TABLE dbo.BedReservations (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BedReservations PRIMARY KEY,
    BedId        INT           NOT NULL CONSTRAINT FK_BedReservations_Beds REFERENCES dbo.Beds (Id),
    InquiryId    INT           NULL CONSTRAINT FK_BedReservations_Inquiries REFERENCES dbo.Inquiries (Id),
    HolderName   NVARCHAR(150) NOT NULL,
    Phone        NVARCHAR(30)  NULL,
    Note         NVARCHAR(500) NULL,
    Status       NVARCHAR(20)  NOT NULL CONSTRAINT DF_BedReservations_Status DEFAULT (N'Active'),
    ReservedById INT           NOT NULL CONSTRAINT FK_BedReservations_Reserver REFERENCES dbo.Accounts (Id),
    ReservedAt   DATETIME2     NOT NULL CONSTRAINT DF_BedReservations_ReservedAt DEFAULT (SYSUTCDATETIME()),
    ExpiresAt    DATETIME2     NOT NULL,
    ClosedAt     DATETIME2     NULL,
    ClosedById   INT           NULL CONSTRAINT FK_BedReservations_Closer REFERENCES dbo.Accounts (Id),
    ResidentId   INT           NULL CONSTRAINT FK_BedReservations_Residents REFERENCES dbo.Residents (Id),
    CONSTRAINT CK_BedReservations_Status CHECK (Status IN (N'Active', N'Converted', N'Released')),
    CONSTRAINT CK_BedReservations_Expiry CHECK (ExpiresAt > ReservedAt)
);
GO

-- Mỗi giường tối đa một lượt giữ đang Active.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_BedReservations_ActiveBed' AND object_id = OBJECT_ID(N'dbo.BedReservations'))
CREATE UNIQUE INDEX UX_BedReservations_ActiveBed ON dbo.BedReservations (BedId) WHERE Status = N'Active';
GO

IF OBJECT_ID(N'dbo.MaintenanceRequests', N'U') IS NULL
CREATE TABLE dbo.MaintenanceRequests (
    Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MaintenanceRequests PRIMARY KEY,
    RoomId         INT            NOT NULL CONSTRAINT FK_MaintenanceRequests_Rooms REFERENCES dbo.Rooms (Id),
    BedId          INT            NULL CONSTRAINT FK_MaintenanceRequests_Beds REFERENCES dbo.Beds (Id),
    Title          NVARCHAR(200)  NOT NULL,
    Description    NVARCHAR(1000) NULL,
    Priority       NVARCHAR(20)   NOT NULL CONSTRAINT DF_MaintenanceRequests_Priority DEFAULT (N'Normal'),
    Status         NVARCHAR(20)   NOT NULL CONSTRAINT DF_MaintenanceRequests_Status DEFAULT (N'Open'),
    BlocksBed      BIT            NOT NULL CONSTRAINT DF_MaintenanceRequests_BlocksBed DEFAULT (0),
    ReportedById   INT            NOT NULL CONSTRAINT FK_MaintenanceRequests_Reporter REFERENCES dbo.Accounts (Id),
    ReportedAt     DATETIME2      NOT NULL CONSTRAINT DF_MaintenanceRequests_ReportedAt DEFAULT (SYSUTCDATETIME()),
    AssignedTo     NVARCHAR(150)  NULL,
    ResolvedAt     DATETIME2      NULL,
    ResolvedById   INT            NULL CONSTRAINT FK_MaintenanceRequests_Resolver REFERENCES dbo.Accounts (Id),
    ResolutionNote NVARCHAR(500)  NULL,
    CONSTRAINT CK_MaintenanceRequests_Priority CHECK (Priority IN (N'Low', N'Normal', N'High')),
    CONSTRAINT CK_MaintenanceRequests_Status CHECK (Status IN (N'Open', N'InProgress', N'Done', N'Cancelled')),
    CONSTRAINT CK_MaintenanceRequests_BlocksBed CHECK (BlocksBed = 0 OR BedId IS NOT NULL)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MaintenanceRequests_Status' AND object_id = OBJECT_ID(N'dbo.MaintenanceRequests'))
CREATE INDEX IX_MaintenanceRequests_Status ON dbo.MaintenanceRequests (Status, ReportedAt DESC);
GO

/* ---- 9. MODULE VẬN HÀNH & TÀI CHÍNH — KẾT THÚC ---- */


/* ===============================================================
   10. MODULE CHĂM SÓC & GIA ĐÌNH — BẮT ĐẦU (Huy, Sprint 3+)
   Sự cố, bệnh nền/dị ứng, chế độ ăn, lịch thăm, kế hoạch chăm sóc, ADL, bữa ăn, đánh giá, phản hồi.
   Chỉ Huy thêm bảng trong vùng này; chèn NGAY TRÊN dòng "KẾT THÚC" bên dưới.
   =============================================================== */

/* S3-H1: sự cố (UC-58, UC-59, UC-107). Viên dùng các cột này cho báo cáo UC-125 — giữ nguyên tên. */
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

/* S3-H2: bệnh nền, dị ứng, chế độ ăn (UC-66, UC-71, UC-82). */
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

/* S3-H3: đặt & duyệt lịch thăm (UC-103, UC-104). */
IF OBJECT_ID(N'dbo.VisitBookings', N'U') IS NULL
CREATE TABLE dbo.VisitBookings (
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_VisitBookings PRIMARY KEY,
    ResidentId      INT           NOT NULL CONSTRAINT FK_VisitBookings_Residents REFERENCES dbo.Residents (Id),
    FamilyAccountId INT           NOT NULL CONSTRAINT FK_VisitBookings_Accounts REFERENCES dbo.Accounts (Id),
    VisitDate       DATE          NOT NULL,
    SlotStart       TIME(0)       NOT NULL,
    SlotEnd         TIME(0)       NOT NULL,
    GuestCount      INT           NOT NULL,
    GuestNames      NVARCHAR(500) NULL,
    Note            NVARCHAR(500) NULL,
    Status          NVARCHAR(20)  NOT NULL CONSTRAINT DF_VisitBookings_Status DEFAULT (N'Requested'),
    ReviewedById    INT           NULL CONSTRAINT FK_VisitBookings_Reviewer REFERENCES dbo.Accounts (Id),
    ReviewNote      NVARCHAR(500) NULL,
    CheckedInAt     DATETIME2     NULL,
    CreatedAt       DATETIME2     NOT NULL CONSTRAINT DF_VisitBookings_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt       DATETIME2     NULL,
    CONSTRAINT CK_VisitBookings_Guests CHECK (GuestCount BETWEEN 1 AND 5),
    CONSTRAINT CK_VisitBookings_Slot CHECK (SlotEnd > SlotStart),
    CONSTRAINT CK_VisitBookings_Status CHECK (Status IN (N'Requested', N'Approved', N'Rejected', N'CheckedIn', N'Completed', N'Cancelled', N'NoShow'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VisitBookings_Date_Slot')
    CREATE INDEX IX_VisitBookings_Date_Slot ON dbo.VisitBookings (VisitDate, SlotStart, Status);
GO

/* S3-H4: kế hoạch chăm sóc tự sinh việc, nhắc việc, ADL, bữa ăn (UC-51, UC-56, UC-57, UC-61). */
IF OBJECT_ID(N'dbo.CarePlans', N'U') IS NULL
CREATE TABLE dbo.CarePlans (
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CarePlans PRIMARY KEY,
    ResidentId  INT            NOT NULL CONSTRAINT FK_CarePlans_Residents REFERENCES dbo.Residents (Id),
    Title       NVARCHAR(200)  NOT NULL,
    Goals       NVARCHAR(1000) NULL,
    StartDate   DATE           NOT NULL,
    EndDate     DATE           NULL,
    IsActive    BIT            NOT NULL CONSTRAINT DF_CarePlans_IsActive DEFAULT (1),
    CreatedById INT            NOT NULL CONSTRAINT FK_CarePlans_Accounts REFERENCES dbo.Accounts (Id),
    CreatedAt   DATETIME2      NOT NULL CONSTRAINT DF_CarePlans_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt   DATETIME2      NULL,
    CONSTRAINT CK_CarePlans_Dates CHECK (EndDate IS NULL OR EndDate >= StartDate)
);
GO
IF OBJECT_ID(N'dbo.CarePlanItems', N'U') IS NULL
CREATE TABLE dbo.CarePlanItems (
    Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CarePlanItems PRIMARY KEY,
    CarePlanId        INT            NOT NULL CONSTRAINT FK_CarePlanItems_Plans REFERENCES dbo.CarePlans (Id),
    Title             NVARCHAR(200)  NOT NULL,
    Instructions      NVARCHAR(1000) NULL,
    TimeOfDay         TIME(0)        NOT NULL,
    DaysOfWeek        CHAR(7)        NOT NULL CONSTRAINT DF_CarePlanItems_Days DEFAULT ('1111111'),
    DefaultAssigneeId INT            NULL CONSTRAINT FK_CarePlanItems_Accounts REFERENCES dbo.Accounts (Id),
    IsActive          BIT            NOT NULL CONSTRAINT DF_CarePlanItems_IsActive DEFAULT (1),
    CONSTRAINT CK_CarePlanItems_Days CHECK (DaysOfWeek NOT LIKE '%[^01]%')
);
GO
IF OBJECT_ID(N'dbo.GeneratedCareTasks', N'U') IS NULL
CREATE TABLE dbo.GeneratedCareTasks (
    CarePlanItemId INT       NOT NULL CONSTRAINT FK_GeneratedCareTasks_Items REFERENCES dbo.CarePlanItems (Id),
    ForDate        DATE      NOT NULL,
    CareTaskId     INT       NOT NULL CONSTRAINT FK_GeneratedCareTasks_Tasks REFERENCES dbo.CareTasks (Id),
    CreatedAt      DATETIME2 NOT NULL CONSTRAINT DF_GeneratedCareTasks_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_GeneratedCareTasks PRIMARY KEY (CarePlanItemId, ForDate)
);
GO
IF OBJECT_ID(N'dbo.TaskReminders', N'U') IS NULL
CREATE TABLE dbo.TaskReminders (
    CareTaskId INT          NOT NULL CONSTRAINT FK_TaskReminders_Tasks REFERENCES dbo.CareTasks (Id),
    Kind       NVARCHAR(10) NOT NULL,
    SentAt     DATETIME2    NOT NULL CONSTRAINT DF_TaskReminders_SentAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_TaskReminders PRIMARY KEY (CareTaskId, Kind),
    CONSTRAINT CK_TaskReminders_Kind CHECK (Kind IN (N'DueSoon', N'Overdue'))
);
GO
IF OBJECT_ID(N'dbo.DailyCareLogs', N'U') IS NULL
CREATE TABLE dbo.DailyCareLogs (
    Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DailyCareLogs PRIMARY KEY,
    ResidentId      INT           NOT NULL CONSTRAINT FK_DailyCareLogs_Residents REFERENCES dbo.Residents (Id),
    LogDate         DATE          NOT NULL,
    Category        NVARCHAR(20)  NOT NULL,
    Level           NVARCHAR(20)  NOT NULL,
    Note            NVARCHAR(500) NULL,
    RecordedById    INT           NOT NULL CONSTRAINT FK_DailyCareLogs_Accounts REFERENCES dbo.Accounts (Id),
    RecordedAt      DATETIME2     NOT NULL CONSTRAINT DF_DailyCareLogs_RecordedAt DEFAULT (SYSUTCDATETIME()),
    ShareWithFamily BIT           NOT NULL CONSTRAINT DF_DailyCareLogs_Share DEFAULT (1),
    CONSTRAINT CK_DailyCareLogs_Category CHECK (Category IN (N'Bathing', N'Toileting', N'Mobility', N'Sleep', N'Mood', N'Other')),
    CONSTRAINT CK_DailyCareLogs_Level CHECK (Level IN (N'Independent', N'NeedsHelp', N'Dependent', N'Good', N'Fair', N'Poor'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DailyCareLogs_Resident_Date')
    CREATE INDEX IX_DailyCareLogs_Resident_Date ON dbo.DailyCareLogs (ResidentId, LogDate);
GO
IF OBJECT_ID(N'dbo.MealLogs', N'U') IS NULL
CREATE TABLE dbo.MealLogs (
    Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MealLogs PRIMARY KEY,
    ResidentId      INT           NOT NULL CONSTRAINT FK_MealLogs_Residents REFERENCES dbo.Residents (Id),
    MealDate        DATE          NOT NULL,
    Meal            NVARCHAR(10)  NOT NULL,
    IntakePercent   INT           NOT NULL,
    FluidMl         INT           NULL,
    Note            NVARCHAR(500) NULL,
    RecordedById    INT           NOT NULL CONSTRAINT FK_MealLogs_Accounts REFERENCES dbo.Accounts (Id),
    RecordedAt      DATETIME2     NOT NULL CONSTRAINT DF_MealLogs_RecordedAt DEFAULT (SYSUTCDATETIME()),
    ShareWithFamily BIT           NOT NULL CONSTRAINT DF_MealLogs_Share DEFAULT (1),
    CONSTRAINT UQ_MealLogs UNIQUE (ResidentId, MealDate, Meal),
    CONSTRAINT CK_MealLogs_Meal CHECK (Meal IN (N'Breakfast', N'Lunch', N'Dinner', N'Snack')),
    CONSTRAINT CK_MealLogs_Intake CHECK (IntakePercent BETWEEN 0 AND 100),
    CONSTRAINT CK_MealLogs_Fluid CHECK (FluidMl IS NULL OR FluidMl BETWEEN 0 AND 5000)
);
GO

/* S3-H5: ngưỡng sinh hiệu riêng, đánh giá định kỳ (Morse/Barthel/MNA-SF), phiếu khám bác sĩ (UC-64, UC-67, UC-68). */
IF OBJECT_ID(N'dbo.VitalThresholds', N'U') IS NULL
CREATE TABLE dbo.VitalThresholds (
    ResidentId      INT           NOT NULL CONSTRAINT PK_VitalThresholds PRIMARY KEY
                                  CONSTRAINT FK_VitalThresholds_Residents REFERENCES dbo.Residents (Id),
    SystolicHigh    INT           NOT NULL,
    SystolicLow     INT           NOT NULL,
    HeartRateHigh   INT           NOT NULL,
    HeartRateLow    INT           NOT NULL,
    TemperatureHigh DECIMAL(4,1)  NOT NULL,
    SpO2Low         DECIMAL(5,2)  NOT NULL,
    Note            NVARCHAR(500) NULL,
    SetById         INT           NOT NULL CONSTRAINT FK_VitalThresholds_Accounts REFERENCES dbo.Accounts (Id),
    UpdatedAt       DATETIME2     NOT NULL CONSTRAINT DF_VitalThresholds_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_VitalThresholds CHECK (SystolicHigh > SystolicLow AND HeartRateHigh > HeartRateLow)
);
GO
IF OBJECT_ID(N'dbo.Assessments', N'U') IS NULL
CREATE TABLE dbo.Assessments (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Assessments PRIMARY KEY,
    ResidentId   INT           NOT NULL CONSTRAINT FK_Assessments_Residents REFERENCES dbo.Residents (Id),
    Type         NVARCHAR(20)  NOT NULL,
    Score        INT           NOT NULL,
    MaxScore     INT           NOT NULL,
    RiskLevel    NVARCHAR(10)  NOT NULL,
    AnswersJson  NVARCHAR(MAX) NOT NULL,
    Note         NVARCHAR(500) NULL,
    AssessedById INT           NOT NULL CONSTRAINT FK_Assessments_Accounts REFERENCES dbo.Accounts (Id),
    AssessedAt   DATETIME2     NOT NULL CONSTRAINT DF_Assessments_AssessedAt DEFAULT (SYSUTCDATETIME()),
    NextDueOn    DATE          NULL,
    CONSTRAINT CK_Assessments_Type CHECK (Type IN (N'FallRisk', N'Barthel', N'Nutrition')),
    CONSTRAINT CK_Assessments_Risk CHECK (RiskLevel IN (N'Low', N'Medium', N'High'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Assessments_Resident_Type')
    CREATE INDEX IX_Assessments_Resident_Type ON dbo.Assessments (ResidentId, Type, AssessedAt DESC);
GO
IF OBJECT_ID(N'dbo.DoctorVisits', N'U') IS NULL
CREATE TABLE dbo.DoctorVisits (
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DoctorVisits PRIMARY KEY,
    ResidentId      INT            NOT NULL CONSTRAINT FK_DoctorVisits_Residents REFERENCES dbo.Residents (Id),
    DoctorId        INT            NOT NULL CONSTRAINT FK_DoctorVisits_Accounts REFERENCES dbo.Accounts (Id),
    VisitedAt       DATETIME2      NOT NULL,
    Subjective      NVARCHAR(2000) NULL,
    Objective       NVARCHAR(2000) NULL,
    Assessment      NVARCHAR(2000) NOT NULL,
    [Plan]          NVARCHAR(2000) NULL,
    FollowUpOn      DATE           NULL,
    ShareWithFamily BIT            NOT NULL CONSTRAINT DF_DoctorVisits_Share DEFAULT (0),
    CreatedAt       DATETIME2      NOT NULL CONSTRAINT DF_DoctorVisits_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt       DATETIME2      NULL
);
GO

/* S3-H6: phản hồi & khiếu nại từ gia đình (UC-109, UC-110). */
IF OBJECT_ID(N'dbo.Feedbacks', N'U') IS NULL
CREATE TABLE dbo.Feedbacks (
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Feedbacks PRIMARY KEY,
    FamilyAccountId INT            NOT NULL CONSTRAINT FK_Feedbacks_Family REFERENCES dbo.Accounts (Id),
    ResidentId      INT            NULL CONSTRAINT FK_Feedbacks_Residents REFERENCES dbo.Residents (Id),
    Category        NVARCHAR(20)   NOT NULL,
    Rating          TINYINT        NULL,
    Subject         NVARCHAR(200)  NOT NULL,
    Content         NVARCHAR(2000) NOT NULL,
    Status          NVARCHAR(20)   NOT NULL CONSTRAINT DF_Feedbacks_Status DEFAULT (N'New'),
    AssignedToId    INT            NULL CONSTRAINT FK_Feedbacks_Assignee REFERENCES dbo.Accounts (Id),
    Response        NVARCHAR(2000) NULL,
    RespondedById   INT            NULL CONSTRAINT FK_Feedbacks_Responder REFERENCES dbo.Accounts (Id),
    RespondedAt     DATETIME2      NULL,
    CreatedAt       DATETIME2      NOT NULL CONSTRAINT DF_Feedbacks_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_Feedbacks_Category CHECK (Category IN (N'Care', N'Food', N'Facility', N'Billing', N'Staff', N'Other')),
    CONSTRAINT CK_Feedbacks_Rating CHECK (Rating IS NULL OR Rating BETWEEN 1 AND 5),
    CONSTRAINT CK_Feedbacks_Status CHECK (Status IN (N'New', N'InProgress', N'Resolved'))
);
GO

/* S4-H2: nhật ký hằng ngày AI (AI-03) & diễn giải xu hướng sinh hiệu (AI-06). */
IF OBJECT_ID(N'dbo.AiInsights', N'U') IS NULL
CREATE TABLE dbo.AiInsights (
    Id           BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AiInsights PRIMARY KEY,
    ResidentId   INT           NOT NULL CONSTRAINT FK_AiInsights_Residents REFERENCES dbo.Residents (Id),
    Type         NVARCHAR(20)  NOT NULL,
    ForDate      DATE          NOT NULL,
    Content      NVARCHAR(MAX) NOT NULL,
    SourceJson   NVARCHAR(MAX) NULL,
    Status       NVARCHAR(10)  NOT NULL CONSTRAINT DF_AiInsights_Status DEFAULT (N'Draft'),
    GeneratedAt  DATETIME2     NOT NULL CONSTRAINT DF_AiInsights_GeneratedAt DEFAULT (SYSUTCDATETIME()),
    ReviewedById INT           NULL CONSTRAINT FK_AiInsights_Reviewer REFERENCES dbo.Accounts (Id),
    ReviewedAt   DATETIME2     NULL,
    CONSTRAINT UQ_AiInsights UNIQUE (ResidentId, Type, ForDate),
    CONSTRAINT CK_AiInsights_Type CHECK (Type IN (N'DailyDigest', N'VitalTrend')),
    CONSTRAINT CK_AiInsights_Status CHECK (Status IN (N'Draft', N'Approved', N'Rejected'))
);
GO

/* ---- 10. MODULE CHĂM SÓC & GIA ĐÌNH — KẾT THÚC ---- */

PRINT N'01_Schema.sql: xong.';
GO
GO

-- ============================================================
-- 02_Views_Functions.sql
-- ============================================================
SET XACT_ABORT OFF; SET NOCOUNT OFF; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;
GO
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
GO

-- ============================================================
-- 03_StoredProcedures.sql
-- ============================================================
SET XACT_ABORT OFF; SET NOCOUNT OFF; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;
GO
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

-- UC-125: sự cố theo tháng × loại × mức độ trong [@From, @To] (ngày xảy ra theo giờ cơ sở). Cột theo bảng Incidents của S3-H1.
CREATE OR ALTER PROCEDURE dbo.sp_IncidentSummary
    @From DATE,
    @To DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT YEAR(DATEADD(HOUR, 7, i.OccurredAt)) AS [Year],
           MONTH(DATEADD(HOUR, 7, i.OccurredAt)) AS [Month],
           i.Type, i.Severity,
           COUNT(*) AS Total,
           SUM(CASE WHEN i.Status = N'Closed' THEN 1 ELSE 0 END) AS Closed
    FROM dbo.Incidents AS i
    WHERE CAST(DATEADD(HOUR, 7, i.OccurredAt) AS DATE) BETWEEN @From AND @To
    GROUP BY YEAR(DATEADD(HOUR, 7, i.OccurredAt)), MONTH(DATEADD(HOUR, 7, i.OccurredAt)), i.Type, i.Severity
    ORDER BY [Year], [Month], i.Type, i.Severity;
END;
GO

PRINT N'03_StoredProcedures.sql: xong.';
GO
GO

-- ============================================================
-- 04_ReferenceData.sql
-- ============================================================
SET XACT_ABORT OFF; SET NOCOUNT OFF; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;
GO
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

-- BL-V3 (UC-36): loại phòng + phụ thu theo ngày (cộng vào hóa đơn tháng theo số ngày ở phòng đó).
IF NOT EXISTS (SELECT 1 FROM dbo.RoomTypes WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.RoomTypes ON;
    INSERT INTO dbo.RoomTypes (Id, Name, DailySurcharge, Description) VALUES
    (1, N'Tiêu chuẩn', 0, N'Phòng 2–3 giường, giá đã gồm trong gói dịch vụ.'),
    (2, N'Phòng đôi tiện nghi', 50000, N'Phòng 2 giường, có ban công và tủ lạnh riêng.'),
    (3, N'Phòng đơn VIP', 150000, N'Phòng riêng, nhà vệ sinh riêng, tivi, người nhà ở lại được.');
    SET IDENTITY_INSERT dbo.RoomTypes OFF;
END;

COMMIT TRANSACTION;
PRINT N'04_ReferenceData.sql: xong.';
GO
GO

-- ============================================================
-- 05_DemoSeed.sql
-- ============================================================
SET XACT_ABORT OFF; SET NOCOUNT OFF; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;
GO
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

-- Nhật ký thuốc 7 ngày trước hôm nay: chỉ thêm buổi CÒN THIẾU (NOT EXISTS theo từng lịch + giờ,
-- có UQ_MedicationLogs_Schedule_Time) nên chạy lại không nhân bản. Không chặn bằng điều kiện theo ngày:
-- DB seed từ mấy hôm trước có log cũ hơn hôm qua và sẽ bị bỏ qua cả khối (lỗi gặp ngày 16/11).
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

/* ---- 10.5 Dịch vụ phát sinh (S3-V5) ----
   3 lần sử dụng tháng này CHƯA vào hóa đơn (NH-0001, NH-0003) để thử "tạo hóa đơn tự thêm dịch vụ phát sinh".
   Danh mục dịch vụ ở 04_ReferenceData.sql. Ngày sử dụng kẹp trong tháng này, không quá hôm nay. */
IF NOT EXISTS (SELECT 1 FROM dbo.ServiceUsages WHERE Id = 1)
BEGIN
    DECLARE @UsageEarly DATE = CASE WHEN DATEADD(DAY, -5, @Today) < @ThisMonth THEN @ThisMonth ELSE DATEADD(DAY, -5, @Today) END;
    DECLARE @UsageLate DATE = CASE WHEN DATEADD(DAY, -1, @Today) < @ThisMonth THEN @ThisMonth ELSE DATEADD(DAY, -1, @Today) END;
    SET IDENTITY_INSERT dbo.ServiceUsages ON;
    INSERT INTO dbo.ServiceUsages (Id, ResidentId, ExtraServiceId, UsedOn, Quantity, UnitPrice, Note, RecordedById, InvoiceItemId, CreatedAt) VALUES
    (1, 1, 1, @UsageEarly, 3.5, 30000, N'Chăn + quần áo', 6, NULL, DATEADD(HOUR, -7, CAST(@UsageEarly AS DATETIME2))),
    (2, 1, 3, @UsageLate, 1, 50000, NULL, 2, NULL, DATEADD(HOUR, -7, CAST(@UsageLate AS DATETIME2))),
    (3, 3, 2, @UsageEarly, 2, 250000, N'Tập phục hồi chức năng chân phải', 5, NULL, DATEADD(HOUR, -7, CAST(@UsageEarly AS DATETIME2)));
    SET IDENTITY_INSERT dbo.ServiceUsages OFF;
END;

/* ---- 10.6 Chat gia đình – nhân viên (S3-V6) ----
   NH-0001 ↔ family (8): gia đình hỏi, điều dưỡng (5) trả lời (đã đọc), tin cuối của gia đình CHƯA đọc
   → nhân viên thấy 1 tin chưa đọc. */
IF NOT EXISTS (SELECT 1 FROM dbo.Conversations WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.Conversations ON;
    INSERT INTO dbo.Conversations (Id, ResidentId, FamilyAccountId, CreatedAt, LastMessageAt) VALUES
    (1, 1, 8, DATEADD(HOUR, -26, @NowUtc), DATEADD(HOUR, -2, @NowUtc));
    SET IDENTITY_INSERT dbo.Conversations OFF;

    INSERT INTO dbo.Messages (ConversationId, SenderId, FromFamily, Body, SentAt, ReadAt) VALUES
    (1, 8, 1, N'Chào cơ sở, hôm qua bố tôi ăn uống có ổn không ạ? Tuần này gia đình muốn mang thêm sữa vào.', DATEADD(HOUR, -26, @NowUtc), DATEADD(HOUR, -25, @NowUtc)),
    (1, 5, 0, N'Chào chị, ông ăn hết suất cháo trưa và ngủ trưa tốt. Gia đình cứ mang sữa vào, gửi ở quầy lễ tân giúp em nhé.', DATEADD(HOUR, -25, @NowUtc), DATEADD(HOUR, -24, @NowUtc)),
    (1, 8, 1, N'Cảm ơn em. Cuối tuần này 9h sáng gia đình vào thăm được không?', DATEADD(HOUR, -2, @NowUtc), NULL);
END;

/* ---- 10.7 Mã mời tài khoản gia đình (BL-V1, UC-08) ----
   NH-0003 chưa có tài khoản gia đình: một mã mời đang chờ, mã demo DEMO-NH0003-MOI
   (mở /kich-hoat?code=DEMO-NH0003-MOI). Hạn 30 ngày kể từ lúc seed; mã tạo trong app hạn 7 ngày. */
IF NOT EXISTS (SELECT 1 FROM dbo.FamilyInvitations WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.FamilyInvitations ON;
    INSERT INTO dbo.FamilyInvitations (Id, CodeHash, Email, ResidentId, Relationship, CanViewHealth, CanViewCare, CanViewMedication, CanViewBilling, CreatedById, CreatedAt, ExpiresAt)
    VALUES (1, CONVERT(CHAR(64), HASHBYTES('SHA2_256', 'DEMO-NH0003-MOI'), 2), N'giadinh.nh0003@example.com', 3, N'Con trai',
            1, 1, 0, 1, 2, @NowUtc, DATEADD(DAY, 30, @NowUtc));
    SET IDENTITY_INSERT dbo.FamilyInvitations OFF;
END;

/* ---- 10.8 Hồ sơ lưu trú (BL-V2) ----
   NH-0001: hợp đồng đang hiệu lực (chưa đính kèm file) + cọc 10 triệu đang giữ.
   NH-0004: đang tạm vắng (nằm viện, dự kiến về sau 3 ngày).
   NH-0005: đã ngừng lưu trú, còn cọc 8 triệu chưa hoàn → thử hoàn cọc / tái nhập viện. */
IF NOT EXISTS (SELECT 1 FROM dbo.ResidentContracts WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.ResidentContracts ON;
    INSERT INTO dbo.ResidentContracts (Id, ResidentId, Number, SignedOn, StartDate, MonthlyPrice, Notes, Status, CreatedById, CreatedAt)
    SELECT 1, r.Id, N'HD-2026-0001', CAST(DATEADD(HOUR, 7, r.AdmittedAt) AS DATE), CAST(DATEADD(HOUR, 7, r.AdmittedAt) AS DATE),
           s.AgreedMonthlyPrice, N'Hợp đồng lưu trú dài hạn (mô phỏng).', N'Active', 2, r.AdmittedAt
    FROM dbo.Residents AS r
    OUTER APPLY (SELECT TOP 1 AgreedMonthlyPrice FROM dbo.ResidentSubscriptions WHERE ResidentId = r.Id ORDER BY StartDate) AS s
    WHERE r.Id = 1;
    SET IDENTITY_INSERT dbo.ResidentContracts OFF;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.ResidentLeaves WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.ResidentLeaves ON;
    INSERT INTO dbo.ResidentLeaves (Id, ResidentId, Kind, StartAt, ExpectedReturnOn, Reason, CreatedById, CreatedAt)
    VALUES (1, 4, N'Hospital', DATEADD(DAY, -1, @NowUtc), DATEADD(DAY, 3, @Today), N'Nhập viện theo dõi tim mạch (mô phỏng).', 2, DATEADD(DAY, -1, @NowUtc));
    SET IDENTITY_INSERT dbo.ResidentLeaves OFF;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Deposits WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.Deposits ON;
    INSERT INTO dbo.Deposits (Id, ResidentId, Amount, Method, Reference, ReceivedAt, ReceivedById)
    SELECT 1, 1, 10000000, N'Cash', N'PT-COC-0001', r.AdmittedAt, 3 FROM dbo.Residents AS r WHERE r.Id = 1
    UNION ALL
    SELECT 2, 5, 8000000, N'BankTransfer', N'CK-COC-0005', r.AdmittedAt, 3 FROM dbo.Residents AS r WHERE r.Id = 5;
    SET IDENTITY_INSERT dbo.Deposits OFF;
END;

/* ---- 10.9 Phòng & giường mở rộng (BL-V3) ----
   Mọi phòng chưa có loại = Tiêu chuẩn (không phụ thu, hóa đơn cũ không đổi). Phòng 202 (đang trống) = Phòng đơn VIP.
   Giữ giường 202-B cho yêu cầu tư vấn số 3 (đã tham quan) 5 ngày. Một phiếu báo hỏng đang mở ở phòng 101. */
UPDATE dbo.Rooms SET RoomTypeId = 1 WHERE RoomTypeId IS NULL AND Number <> N'202';
UPDATE dbo.Rooms SET RoomTypeId = 3 WHERE RoomTypeId IS NULL AND Number = N'202';

IF NOT EXISTS (SELECT 1 FROM dbo.BedReservations WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.BedReservations ON;
    INSERT INTO dbo.BedReservations (Id, BedId, InquiryId, HolderName, Phone, Note, Status, ReservedById, ReservedAt, ExpiresAt)
    SELECT 1, b.Id, i.Id, i.FullName, i.Phone, N'Gia đình hẹn làm thủ tục nhập viện cuối tuần.', N'Active', 2, @NowUtc, DATEADD(DAY, 5, @NowUtc)
    FROM dbo.Beds AS b JOIN dbo.Rooms AS r ON r.Id = b.RoomId
    CROSS JOIN (SELECT Id, FullName, Phone FROM dbo.Inquiries WHERE Id = 3) AS i
    WHERE r.Number = N'202' AND b.Number = N'B' AND b.Status = N'Available';
    SET IDENTITY_INSERT dbo.BedReservations OFF;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.MaintenanceRequests WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.MaintenanceRequests ON;
    INSERT INTO dbo.MaintenanceRequests (Id, RoomId, Title, Description, Priority, Status, ReportedById, ReportedAt)
    SELECT 1, r.Id, N'Bóng đèn nhà vệ sinh chập chờn', N'Ban đêm cư dân đi vệ sinh dễ té ngã, cần thay sớm.', N'Normal', N'Open', 6, DATEADD(HOUR, -3, @NowUtc)
    FROM dbo.Rooms AS r WHERE r.Number = N'101';
    SET IDENTITY_INSERT dbo.MaintenanceRequests OFF;
END;

/* ---- 10. MODULE VẬN HÀNH & TÀI CHÍNH — KẾT THÚC ---- */


/* ===============================================================
   11. MODULE CHĂM SÓC & GIA ĐÌNH — BẮT ĐẦU (Huy)
   Dải Id dành cho dữ liệu demo Huy thêm vào BẢNG DÙNG CHUNG: 500–899.
   Chèn NGAY TRÊN dòng "KẾT THÚC".
   =============================================================== */

/* S3-H1: 1 sự cố đã đóng + chia sẻ gia đình (NH-0002), 1 sự cố đang mở (NH-0001). */
IF NOT EXISTS (SELECT 1 FROM dbo.Incidents WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.Incidents ON;
    INSERT INTO dbo.Incidents (Id, ResidentId, Type, Severity, OccurredAt, Location, Description, ImmediateAction, ReportedById, Status, Resolution, ClosedById, ClosedAt, ShareWithFamily) VALUES
    (1, 2, N'Fall', N'Medium', DATEADD(DAY, -3, DATEADD(HOUR, 3, @TodayUtc)), N'Phòng 101', N'Bà trượt chân khi đứng dậy, không chấn thương (mô phỏng).', N'Đỡ dậy, kiểm tra, theo dõi 2 giờ.', 6, N'Closed', N'Đã lắp tay vịn cạnh giường.', 5, DATEADD(DAY, -2, @TodayUtc), 1),
    (2, 1, N'Behavior', N'Low', DATEADD(HOUR, 2, @TodayUtc), N'Phòng sinh hoạt chung', N'Ông lo lắng, đòi về nhà (mô phỏng).', N'Trò chuyện, gọi người nhà.', 6, N'Open', NULL, NULL, NULL, 0);
    SET IDENTITY_INSERT dbo.Incidents OFF;
END;

/* S3-H2: bệnh nền, dị ứng, chế độ ăn mẫu. */
IF NOT EXISTS (SELECT 1 FROM dbo.MedicalConditions WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.MedicalConditions ON;
    INSERT INTO dbo.MedicalConditions (Id, ResidentId, Name, DiagnosedOn, Status, Notes, RecordedById) VALUES
    (1, 1, N'Tăng huyết áp', DATEADD(YEAR, -4, @Today), N'Active', N'Theo dõi huyết áp sáng/chiều mỗi ngày.', 4);
    SET IDENTITY_INSERT dbo.MedicalConditions OFF;
END;
IF NOT EXISTS (SELECT 1 FROM dbo.Allergies WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.Allergies ON;
    INSERT INTO dbo.Allergies (Id, ResidentId, Allergen, Type, Reaction, Severity, RecordedById) VALUES
    (1, 1, N'Penicillin', N'Drug', N'Phát ban, mẩn đỏ toàn thân', N'Severe', 4),
    (2, 2, N'Tôm', N'Food', N'Nổi mề đay nhẹ', N'Moderate', 5);
    SET IDENTITY_INSERT dbo.Allergies OFF;
END;
IF NOT EXISTS (SELECT 1 FROM dbo.DietPlans WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.DietPlans ON;
    INSERT INTO dbo.DietPlans (Id, ResidentId, DietType, Texture, Restrictions, StartDate, EndDate, PrescribedById) VALUES
    (1, 4, N'Diabetic', N'Soft', N'Hạn chế đường, chia nhỏ 5 bữa/ngày.', DATEADD(DAY, -14, @Today), NULL, 4);
    SET IDENTITY_INSERT dbo.DietPlans OFF;
END;

/* S3-H3: lịch thăm mẫu. */
IF NOT EXISTS (SELECT 1 FROM dbo.VisitBookings WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.VisitBookings ON;
    INSERT INTO dbo.VisitBookings (Id, ResidentId, FamilyAccountId, VisitDate, SlotStart, SlotEnd, GuestCount, GuestNames, Note, Status, ReviewedById, ReviewNote, CheckedInAt) VALUES
    (1, 1, 8, DATEADD(DAY, 1, @Today), '09:00:00', '10:00:00', 2, N'Nguyễn Thị Bích, Nguyễn Văn An', N'Mang sữa cho cụ.', N'Approved', 2, N'Đã xác nhận lịch thăm.', NULL),
    (2, 1, 8, DATEADD(DAY, -7, @Today), '14:00:00', '15:00:00', 2, N'Nguyễn Thị Bích', NULL, N'Completed', 2, NULL, DATEADD(DAY, -7, DATEADD(HOUR, 7, @TodayUtc))),
    (3, 2, 9, DATEADD(DAY, 1, @Today), '15:00:00', '16:00:00', 1, N'Trần Quốc Bảo', N'Thăm buổi chiều.', N'Requested', NULL, NULL, NULL);
    SET IDENTITY_INSERT dbo.VisitBookings OFF;
END;

/* S3-H4: kế hoạch chăm sóc (không seed GeneratedCareTasks để job/nút tự sinh) + ADL & bữa ăn hôm qua. */
IF NOT EXISTS (SELECT 1 FROM dbo.CarePlans WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.CarePlans ON;
    INSERT INTO dbo.CarePlans (Id, ResidentId, Title, Goals, StartDate, EndDate, IsActive, CreatedById) VALUES
    (1, 4, N'Theo dõi đường huyết & vệ sinh', N'Kiểm soát đường huyết ổn định, hỗ trợ sinh hoạt hằng ngày.', DATEADD(DAY, -7, @Today), NULL, 1, 5);
    SET IDENTITY_INSERT dbo.CarePlans OFF;
END;
IF NOT EXISTS (SELECT 1 FROM dbo.CarePlanItems WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.CarePlanItems ON;
    INSERT INTO dbo.CarePlanItems (Id, CarePlanId, Title, Instructions, TimeOfDay, DaysOfWeek, DefaultAssigneeId, IsActive) VALUES
    (1, 1, N'Đo đường huyết sáng', N'Đo trước khi ăn sáng, ghi chỉ số vào hồ sơ.', '06:30:00', '1111111', 7, 1),
    (2, 1, N'Tắm & vệ sinh cá nhân', N'Nước ấm, hỗ trợ di chuyển phòng tắm.', '09:00:00', '1010100', 7, 1);
    SET IDENTITY_INSERT dbo.CarePlanItems OFF;
END;
IF NOT EXISTS (SELECT 1 FROM dbo.DailyCareLogs WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.DailyCareLogs ON;
    INSERT INTO dbo.DailyCareLogs (Id, ResidentId, LogDate, Category, Level, Note, RecordedById, ShareWithFamily) VALUES
    (1, 1, DATEADD(DAY, -1, @Today), N'Mobility', N'NeedsHelp', N'Đi dạo sân vườn 15 phút có hỗ trợ.', 6, 1),
    (2, 2, DATEADD(DAY, -1, @Today), N'Sleep', N'Good', N'Ngủ đủ 7 tiếng, không tỉnh giấc.', 6, 1);
    SET IDENTITY_INSERT dbo.DailyCareLogs OFF;
END;
IF NOT EXISTS (SELECT 1 FROM dbo.MealLogs WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.MealLogs ON;
    INSERT INTO dbo.MealLogs (Id, ResidentId, MealDate, Meal, IntakePercent, FluidMl, Note, RecordedById, ShareWithFamily) VALUES
    (1, 1, DATEADD(DAY, -1, @Today), N'Lunch', 75, 250, N'Ăn cháo thịt bằm, uống nước ấm.', 6, 1),
    (2, 2, DATEADD(DAY, -1, @Today), N'Lunch', 100, 300, N'Ăn hết suất cơm mềm.', 6, 1);
    SET IDENTITY_INSERT dbo.MealLogs OFF;
END;
IF NOT EXISTS (SELECT 1 FROM dbo.MealLogs WHERE Id = 3)
BEGIN
    SET IDENTITY_INSERT dbo.MealLogs ON;
    INSERT INTO dbo.MealLogs (Id, ResidentId, MealDate, Meal, IntakePercent, FluidMl, Note, RecordedById, ShareWithFamily) VALUES
    (3, 1, DATEADD(DAY, -2, @Today), N'Lunch', 85, 280, N'Ăn súp gà, uống đủ nước.', 6, 1),
    (4, 2, DATEADD(DAY, -2, @Today), N'Lunch', 90, 260, N'Ăn hết phần cá kho mềm.', 6, 1),
    (5, 1, DATEADD(DAY, -3, @Today), N'Dinner', 80, 220, N'Ăn cháo rau củ, tinh thần tốt.', 6, 1);
    SET IDENTITY_INSERT dbo.MealLogs OFF;
END;

/* S3-H5: ngưỡng riêng NH-0001 (SystolicHigh = 150), đánh giá mẫu, phiếu khám bác sĩ mẫu. */
IF NOT EXISTS (SELECT 1 FROM dbo.VitalThresholds WHERE ResidentId = 1)
BEGIN
    INSERT INTO dbo.VitalThresholds (ResidentId, SystolicHigh, SystolicLow, HeartRateHigh, HeartRateLow, TemperatureHigh, SpO2Low, Note, SetById) VALUES
    (1, 150, 90, 120, 50, 38.0, 92.00, N'Cụ có tiền sử tăng huyết áp, đặt ngưỡng trên 150 mmHg.', 4);
END;
IF NOT EXISTS (SELECT 1 FROM dbo.Assessments WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.Assessments ON;
    INSERT INTO dbo.Assessments (Id, ResidentId, Type, Score, MaxScore, RiskLevel, AnswersJson, Note, AssessedById, AssessedAt, NextDueOn) VALUES
    (1, 2, N'FallRisk', 45, 125, N'High', N'{"history":"yes","secondary":"yes","aid":"none","iv":"no","gait":"weak","mental":"oriented"}', N'Nguy cơ té ngã cao, đã quá hạn đánh giá lại.', 5, DATEADD(DAY, -100, @TodayUtc), DATEADD(DAY, -10, @Today)),
    (2, 1, N'Barthel', 85, 100, N'Medium', N'{"feeding":"10","bathing":"5","grooming":"5","dressing":"10","bowels":"10","bladder":"10","toilet":"10","transfer":"10","mobility":"10","stairs":"5"}', N'Phụ thuộc nhẹ khi lên cầu thang.', 5, DATEADD(DAY, -20, @TodayUtc), DATEADD(DAY, 70, @Today));
    SET IDENTITY_INSERT dbo.Assessments OFF;
END;
IF NOT EXISTS (SELECT 1 FROM dbo.DoctorVisits WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.DoctorVisits ON;
    INSERT INTO dbo.DoctorVisits (Id, ResidentId, DoctorId, VisitedAt, Subjective, Objective, Assessment, [Plan], FollowUpOn, ShareWithFamily, CreatedAt) VALUES
    (1, 3, 4, DATEADD(DAY, -7, @TodayUtc), N'Ngủ ngon, ăn uống bình thường.', N'Tim đều, phổi trong, huyết áp 125/78 mmHg.', N'Tình trạng ổn định.', N'Tiếp tục chế độ sinh hoạt hiện tại.', DATEADD(DAY, 23, @Today), 1, DATEADD(DAY, -7, @TodayUtc));
    SET IDENTITY_INSERT dbo.DoctorVisits OFF;
END;

/* S3-H6: phản hồi gia đình mẫu. */
IF NOT EXISTS (SELECT 1 FROM dbo.Feedbacks WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.Feedbacks ON;
    INSERT INTO dbo.Feedbacks (Id, FamilyAccountId, ResidentId, Category, Rating, Subject, Content, Status, AssignedToId, Response, RespondedById, RespondedAt) VALUES
    (1, 8, 1, N'Food', 4, N'Bữa ăn mềm dễ nuốt', N'Gia đình thấy thực đơn cháo tuần này rất vừa miệng ông.', N'Resolved', 5, N'Cảm ơn gia đình đã góp ý, bếp sẽ duy trì thực đơn này.', 1, DATEADD(DAY, -1, @NowUtc)),
    (2, 9, 2, N'Facility', NULL, N'Đèn hành lang tầng 1', N'Buổi tối đèn hành lang hơi tối, nhờ cơ sở kiểm tra thêm.', N'New', NULL, NULL, NULL, NULL);
    SET IDENTITY_INSERT dbo.Feedbacks OFF;
END;

/* S4-H2 & S5-H1: nhật ký AI đã duyệt hôm qua cho NH-0001. */
IF NOT EXISTS (SELECT 1 FROM dbo.AiInsights WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.AiInsights ON;
    INSERT INTO dbo.AiInsights (Id, ResidentId, Type, ForDate, Content, Status, ReviewedById, ReviewedAt, GeneratedAt) VALUES
    (1, 1, N'DailyDigest', DATEADD(DAY, -1, @Today), N'Hôm qua cụ ăn trưa đạt 75% khẩu phần (cháo thịt bằm) và uống khoảng 250 ml nước ấm. Cụ đi dạo sân vườn 15 phút có điều dưỡng hỗ trợ, tinh thần ổn định và hợp tác tốt.', N'Approved', 5, DATEADD(HOUR, -12, @NowUtc), DATEADD(HOUR, -13, @NowUtc));
    SET IDENTITY_INSERT dbo.AiInsights OFF;
END;

/* ---- 11. MODULE CHĂM SÓC & GIA ĐÌNH — KẾT THÚC ---- */

COMMIT TRANSACTION;
PRINT N'05_DemoSeed.sql: xong.';
GO
GO

PRINT N'NursingHomeDB_AllInOne.sql: xong.';
GO
