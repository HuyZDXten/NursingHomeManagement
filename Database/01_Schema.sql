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

/* ---- 9. MODULE VẬN HÀNH & TÀI CHÍNH — KẾT THÚC ---- */


/* ===============================================================
   10. MODULE CHĂM SÓC & GIA ĐÌNH — BẮT ĐẦU (Huy, Sprint 3+)
   Sự cố, bệnh nền/dị ứng, chế độ ăn, lịch thăm, kế hoạch chăm sóc, ADL, bữa ăn, đánh giá, phản hồi.
   Chỉ Huy thêm bảng trong vùng này; chèn NGAY TRÊN dòng "KẾT THÚC" bên dưới.
   =============================================================== */

/* ---- 10. MODULE CHĂM SÓC & GIA ĐÌNH — KẾT THÚC ---- */

PRINT N'01_Schema.sql: xong.';
GO
