namespace NursingHome.BusinessObjects.Enums;

/// <summary>
/// Vai trò lưu dạng chuỗi trong cột Accounts.Role (CHECK constraint ở 01_Schema.sql).
/// Thêm role mới: sửa CHECK trong schema + hằng số ở đây + FE src/types.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Receptionist = "Receptionist";
    public const string Accountant = "Accountant";
    public const string Doctor = "Doctor";
    public const string Nurse = "Nurse";
    public const string Caregiver = "Caregiver";
    public const string Family = "Family";

    public static readonly string[] All = [Admin, Receptionist, Accountant, Doctor, Nurse, Caregiver, Family];

    /// <summary>Nhân viên cơ sở (mọi role trừ Family).</summary>
    public static readonly string[] Staff = [Admin, Receptionist, Accountant, Doctor, Nurse, Caregiver];

    /// <summary>Người được giao công việc chăm sóc.</summary>
    public static readonly string[] CareAssignees = [Nurse, Caregiver];

    public static bool IsValid(string role) => All.Contains(role);
}

public enum ResidentStatus
{
    Active,
    Discharged
}

public enum BedStatus
{
    Available,
    Occupied,
    Maintenance
}

public enum CareTaskStatus
{
    Pending,
    InProgress,
    Completed,
    Cancelled
}

public enum MedicationStatus
{
    Scheduled,
    Taken,
    Missed,
    Refused
}

public enum InvoiceStatus
{
    Draft,
    Unpaid,
    PartiallyPaid,
    Paid,
    Cancelled
}

public enum PaymentMethod
{
    Cash,
    BankTransfer,
    PayOS
}

public enum PaymentTransactionStatus
{
    Pending,
    Paid,
    Expired,
    Cancelled,
    Failed
}

public enum NotificationType
{
    Payment,
    Invoice,
    Care,
    Health,
    System
}

/// <summary>Kết quả xử lý một báo có từ PayOS (cột PaymentWebhookLogs.Outcome).</summary>
public static class PaymentOutcomes
{
    public const string Applied = "APPLIED";
    public const string Duplicate = "DUPLICATE";
    public const string Unmatched = "UNMATCHED";
    public const string Rejected = "REJECTED";
    public const string Surplus = "SURPLUS";
    public const string Ignored = "IGNORED";
}
