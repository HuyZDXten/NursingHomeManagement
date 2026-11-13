using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Care;

/// <summary>Entity set OData "CareTasks".</summary>
public class CareTaskDto
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public string ResidentName { get; set; } = "";
    public string ResidentCode { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Instructions { get; set; }
    public int AssignedToId { get; set; }
    public string AssignedToName { get; set; } = "";
    public DateTime DueAt { get; set; }
    public string Status { get; set; } = "";

    /// <summary>Base64 của rowversion: gửi lại khi cập nhật để chống ghi đè.</summary>
    public string RowVersion { get; set; } = "";
}

public class CreateCareTaskRequest
{
    [Range(1, int.MaxValue)]
    public int ResidentId { get; set; }

    [Range(1, int.MaxValue)]
    public int AssignedToId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(2000)]
    public string? Instructions { get; set; }

    /// <summary>Ngày giờ theo giờ cơ sở (UTC+7).</summary>
    [Required]
    public DateOnly DueDate { get; set; }

    [Required]
    public TimeOnly DueTime { get; set; }
}

public class UpdateCareTaskRequest
{
    /// <summary>InProgress, Completed hoặc Cancelled (chỉ Admin).</summary>
    [Required]
    public string Status { get; set; } = "";

    [Required, MaxLength(2000)]
    public string Result { get; set; } = "";

    public bool ShareWithFamily { get; set; }

    [Required]
    public string RowVersion { get; set; } = "";
}

public class CareRecordDto
{
    public int Id { get; set; }
    public int CareTaskId { get; set; }
    public string TaskTitle { get; set; } = "";
    public string Status { get; set; } = "";
    public string Result { get; set; } = "";
    public DateTime PerformedAt { get; set; }
    public string RecordedByName { get; set; } = "";
    public bool ShareWithFamily { get; set; }
}

public class VitalSignDto
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public int Systolic { get; set; }
    public int Diastolic { get; set; }
    public int HeartRate { get; set; }
    public decimal Temperature { get; set; }
    public decimal? SpO2 { get; set; }
    public string? Notes { get; set; }
    public DateTime RecordedAt { get; set; }
    public string RecordedByName { get; set; } = "";
    public bool ShareWithFamily { get; set; }
}

public class RecordVitalSignRequest
{
    [Range(1, int.MaxValue)]
    public int ResidentId { get; set; }

    [Range(40, 300)]
    public int Systolic { get; set; }

    [Range(20, 200)]
    public int Diastolic { get; set; }

    [Range(20, 250)]
    public int HeartRate { get; set; }

    [Range(typeof(decimal), "25", "45")]
    public decimal Temperature { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal? SpO2 { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public bool ShareWithFamily { get; set; }
}
