using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Care;

/// <summary>S3-H1: sự cố. Type/Severity/Status trả dạng chuỗi tên enum (Fall, High, Open…).</summary>
public class IncidentDto
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public string ResidentName { get; set; } = "";
    public string ResidentCode { get; set; } = "";
    public string Type { get; set; } = "";
    public string Severity { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string? Location { get; set; }
    public string Description { get; set; } = "";
    public string? ImmediateAction { get; set; }
    public string ReportedByName { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Resolution { get; set; }
    public string? ClosedByName { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool ShareWithFamily { get; set; }
    public DateTime? FamilyNotifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Base64 của rowversion: gửi lại khi cập nhật để chống ghi đè.</summary>
    public string RowVersion { get; set; } = "";
}

/// <summary>Bản rút gọn cho trang người thân: không lộ người báo/người đóng.</summary>
public class FamilyIncidentDto
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string Severity { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string? Location { get; set; }
    public string Description { get; set; } = "";
    public string? ImmediateAction { get; set; }
    public string Status { get; set; } = "";
    public string? Resolution { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public class ReportIncidentRequest
{
    [Range(1, int.MaxValue)]
    public int ResidentId { get; set; }

    /// <summary>Fall, Injury, Behavior hoặc Other.</summary>
    [Required]
    public string Type { get; set; } = "";

    /// <summary>Low, Medium hoặc High.</summary>
    [Required]
    public string Severity { get; set; } = "";

    /// <summary>Thời điểm xảy ra (ISO, UTC). Không được ở tương lai.</summary>
    [Required]
    public DateTime OccurredAt { get; set; }

    [MaxLength(100)]
    public string? Location { get; set; }

    [Required, MaxLength(2000)]
    public string Description { get; set; } = "";

    [MaxLength(2000)]
    public string? ImmediateAction { get; set; }
}

public class UpdateIncidentRequest
{
    /// <summary>Investigating hoặc Closed.</summary>
    [Required]
    public string Status { get; set; } = "";

    /// <summary>Bắt buộc khi đóng.</summary>
    [MaxLength(2000)]
    public string? Resolution { get; set; }

    public bool ShareWithFamily { get; set; }

    /// <summary>Chỉ khi đóng và ShareWithFamily = true: báo cho gia đình có quyền CanViewCare.</summary>
    public bool NotifyFamily { get; set; }

    [Required]
    public string RowVersion { get; set; } = "";
}
