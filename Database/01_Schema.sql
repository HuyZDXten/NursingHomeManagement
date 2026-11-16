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
