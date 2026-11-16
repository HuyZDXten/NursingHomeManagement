namespace NursingHome.BusinessObjects.Entities;

public enum InsightType
{
    DailyDigest,
    VitalTrend
}

public enum InsightStatus
{
    Draft,
    Approved,
    Rejected
}

/// <summary>S4-H2 (AI-03, AI-06): bản nháp nhật ký AI gửi gia đình và diễn giải xu hướng sinh hiệu.</summary>
public class AiInsight
{
    public long Id { get; set; }
    public int ResidentId { get; set; }
    public Resident Resident { get; set; } = null!;
    public InsightType Type { get; set; }
    public DateOnly ForDate { get; set; }
    public string Content { get; set; } = "";
    public string? SourceJson { get; set; }
    public InsightStatus Status { get; set; } = InsightStatus.Draft;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int? ReviewedById { get; set; }
    public Account? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
