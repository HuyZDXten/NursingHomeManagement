namespace NursingHome.BusinessObjects.Entities;

public enum AssessmentType
{
    FallRisk,
    Barthel,
    Nutrition
}

public enum RiskLevel
{
    Low,
    Medium,
    High
}

/// <summary>S3-H5 (UC-68): ngưỡng cảnh báo sinh hiệu riêng cho từng cư dân.</summary>
public class VitalThreshold
{
    public int ResidentId { get; set; }
    public Resident Resident { get; set; } = null!;
    public int SystolicHigh { get; set; }
    public int SystolicLow { get; set; }
    public int HeartRateHigh { get; set; }
    public int HeartRateLow { get; set; }
    public decimal TemperatureHigh { get; set; }
    public decimal SpO2Low { get; set; }
    public string? Note { get; set; }
    public int SetById { get; set; }
    public Account SetBy { get; set; } = null!;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>S3-H5 (UC-64): đánh giá sàng lọc định kỳ (Morse, Barthel, MNA-SF).</summary>
public class Assessment
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public Resident Resident { get; set; } = null!;
    public AssessmentType Type { get; set; }
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public string AnswersJson { get; set; } = "{}";
    public string? Note { get; set; }
    public int AssessedById { get; set; }
    public Account AssessedBy { get; set; } = null!;
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
    public DateOnly? NextDueOn { get; set; }
}

/// <summary>S3-H5 (UC-67): phiếu khám bác sĩ theo SOAP.</summary>
public class DoctorVisit
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public Resident Resident { get; set; } = null!;
    public int DoctorId { get; set; }
    public Account Doctor { get; set; } = null!;
    public DateTime VisitedAt { get; set; } = DateTime.UtcNow;
    public string? Subjective { get; set; }
    public string? Objective { get; set; }
    public string Assessment { get; set; } = "";
    public string? Plan { get; set; }
    public DateOnly? FollowUpOn { get; set; }
    public bool ShareWithFamily { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
