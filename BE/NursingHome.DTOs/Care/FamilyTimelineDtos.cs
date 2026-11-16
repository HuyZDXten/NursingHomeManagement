using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Care;

/// <summary>S3-H6 (UC-108): một mục trên dòng thời gian hằng ngày cho gia đình.</summary>
public class TimelineItemDto
{
    /// <summary>Meal, Adl, Care, Incident, Vital hoặc DoctorVisit.</summary>
    public string Kind { get; set; } = "";
    public DateTime At { get; set; }
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string RecordedBy { get; set; } = "";
}

/// <summary>S3-H6 (UC-109, UC-110): phản hồi / khiếu nại từ gia đình.</summary>
public class FeedbackDto
{
    public int Id { get; set; }
    public int FamilyAccountId { get; set; }
    public string FamilyName { get; set; } = "";
    public int? ResidentId { get; set; }
    public string? ResidentName { get; set; }
    public string Category { get; set; } = "";
    public byte? Rating { get; set; }
    public string Subject { get; set; } = "";
    public string Content { get; set; } = "";
    public string Status { get; set; } = "";
    public int? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public string? Response { get; set; }
    public string? RespondedByName { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateFeedbackRequest
{
    public int? ResidentId { get; set; }

    /// <summary>Care, Food, Facility, Billing, Staff hoặc Other.</summary>
    [Required]
    public string Category { get; set; } = "";

    [Range(1, 5)]
    public byte? Rating { get; set; }

    [Required, MaxLength(200)]
    public string Subject { get; set; } = "";

    [Required, MaxLength(2000)]
    public string Content { get; set; } = "";
}

public class RespondFeedbackRequest
{
    /// <summary>InProgress hoặc Resolved.</summary>
    [Required]
    public string Status { get; set; } = "Resolved";

    public int? AssignedToId { get; set; }

    [MaxLength(2000)]
    public string? Response { get; set; }
}
