namespace NursingHome.BusinessObjects.Entities;

public enum FeedbackCategory
{
    Care,
    Food,
    Facility,
    Billing,
    Staff,
    Other
}

public enum FeedbackStatus
{
    New,
    InProgress,
    Resolved
}

/// <summary>S3-H6 (UC-109, UC-110): phản hồi / khiếu nại từ gia đình.</summary>
public class Feedback
{
    public int Id { get; set; }
    public int FamilyAccountId { get; set; }
    public Account FamilyAccount { get; set; } = null!;
    public int? ResidentId { get; set; }
    public Resident? Resident { get; set; }
    public FeedbackCategory Category { get; set; }
    public byte? Rating { get; set; }
    public string Subject { get; set; } = "";
    public string Content { get; set; } = "";
    public FeedbackStatus Status { get; set; } = FeedbackStatus.New;
    public int? AssignedToId { get; set; }
    public Account? AssignedTo { get; set; }
    public string? Response { get; set; }
    public int? RespondedById { get; set; }
    public Account? RespondedBy { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
