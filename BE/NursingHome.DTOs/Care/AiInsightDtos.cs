using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Care;

/// <summary>S4-H2 (AI-03, AI-06): bản nháp nhật ký AI và diễn giải xu hướng sinh hiệu.</summary>
public class AiInsightDto
{
    public long Id { get; set; }
    public int ResidentId { get; set; }
    public string ResidentCode { get; set; } = "";
    public string ResidentName { get; set; } = "";
    public string Type { get; set; } = "";
    public DateOnly ForDate { get; set; }
    public string Content { get; set; } = "";
    public string? SourceJson { get; set; }
    public string Status { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public bool IsAiGenerated { get; set; } = true;
    public string Disclaimer { get; set; } = "Do AI soạn, không thay thế đánh giá của nhân viên y tế.";
}

public class ApproveInsightRequest
{
    [Required, MaxLength(4000)]
    public string Content { get; set; } = "";
}

public class GenerateInsightsResultDto
{
    public int CreatedCount { get; set; }
}
