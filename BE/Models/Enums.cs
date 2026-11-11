namespace NursingHomeManagement.Models;

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

public static class Roles
{
    public const string Admin = "Admin";

    public const string Receptionist = "Receptionist";

    public const string Caregiver = "Caregiver";

    public const string Family = "Family";

    public static readonly string[] All = [Admin, Receptionist, Caregiver, Family];
}
