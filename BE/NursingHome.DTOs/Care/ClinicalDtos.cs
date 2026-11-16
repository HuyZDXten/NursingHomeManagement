using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Care;

public class AssessmentOptionDto
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public int Points { get; set; }
}

public class AssessmentQuestionDto
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public List<AssessmentOptionDto> Options { get; set; } = [];
}

public class AssessmentScaleDto
{
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public int MaxScore { get; set; }
    public string Note { get; set; } = "Công cụ sàng lọc, không thay thế chẩn đoán.";
    public List<AssessmentQuestionDto> Questions { get; set; } = [];
}

public class SubmitAssessmentRequest
{
    [Range(1, int.MaxValue)]
    public int ResidentId { get; set; }

    /// <summary>FallRisk, Barthel hoặc Nutrition.</summary>
    [Required]
    public string Type { get; set; } = "";

    [Required]
    public Dictionary<string, string> Answers { get; set; } = [];

    [MaxLength(500)]
    public string? Note { get; set; }
}

public class AssessmentDto
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public string Type { get; set; } = "";
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public string RiskLevel { get; set; } = "";
    public Dictionary<string, string> Answers { get; set; } = [];
    public string? Note { get; set; }
    public string AssessedByName { get; set; } = "";
    public DateTime AssessedAt { get; set; }
    public DateOnly? NextDueOn { get; set; }
}

public class DueAssessmentDto
{
    public int ResidentId { get; set; }
    public string ResidentCode { get; set; } = "";
    public string ResidentName { get; set; } = "";
    public string Type { get; set; } = "";
    public DateTime? LastAssessedAt { get; set; }
    public string? LastRiskLevel { get; set; }
    public DateOnly? NextDueOn { get; set; }
}

public class DoctorVisitDto
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = "";
    public DateTime VisitedAt { get; set; }
    public string? Subjective { get; set; }
    public string? Objective { get; set; }
    public string Assessment { get; set; } = "";
    public string? Plan { get; set; }
    public DateOnly? FollowUpOn { get; set; }
    public bool ShareWithFamily { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class SaveDoctorVisitRequest
{
    public DateTime? VisitedAt { get; set; }

    [MaxLength(2000)]
    public string? Subjective { get; set; }

    [MaxLength(2000)]
    public string? Objective { get; set; }

    [Required, MaxLength(2000)]
    public string Assessment { get; set; } = "";

    [MaxLength(2000)]
    public string? Plan { get; set; }

    public DateOnly? FollowUpOn { get; set; }

    public bool ShareWithFamily { get; set; }
}

public class ThresholdDto
{
    public int ResidentId { get; set; }
    public bool IsCustom { get; set; }
    public int SystolicHigh { get; set; }
    public int SystolicLow { get; set; }
    public int HeartRateHigh { get; set; }
    public int HeartRateLow { get; set; }
    public decimal TemperatureHigh { get; set; }
    public decimal SpO2Low { get; set; }
    public string? Note { get; set; }
    public string? SetByName { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class SaveThresholdRequest
{
    [Range(90, 260)]
    public int SystolicHigh { get; set; }

    [Range(50, 160)]
    public int SystolicLow { get; set; }

    [Range(60, 220)]
    public int HeartRateHigh { get; set; }

    [Range(30, 100)]
    public int HeartRateLow { get; set; }

    [Range(typeof(decimal), "35.0", "42.0", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal TemperatureHigh { get; set; }

    [Range(typeof(decimal), "70.0", "99.0", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal SpO2Low { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}
