namespace NursingHome.BusinessObjects.Entities;

/// <summary>S3-H3: Requested → Approved/Rejected/Cancelled; Approved → CheckedIn/Cancelled/NoShow; CheckedIn → Completed.</summary>
public enum VisitStatus
{
    Requested,
    Approved,
    Rejected,
    CheckedIn,
    Completed,
    Cancelled,
    NoShow
}

/// <summary>S3-H3 (UC-103, UC-104): lịch thăm của gia đình.</summary>
public class VisitBooking
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public Resident Resident { get; set; } = null!;
    public int FamilyAccountId { get; set; }
    public Account FamilyAccount { get; set; } = null!;
    public DateOnly VisitDate { get; set; }
    public TimeOnly SlotStart { get; set; }
    public TimeOnly SlotEnd { get; set; }
    public int GuestCount { get; set; }
    public string? GuestNames { get; set; }
    public string? Note { get; set; }
    public VisitStatus Status { get; set; } = VisitStatus.Requested;
    public int? ReviewedById { get; set; }
    public Account? ReviewedBy { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
