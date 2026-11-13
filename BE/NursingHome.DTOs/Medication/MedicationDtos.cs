using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Medication;

public class MedicationScheduleDto
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public string ResidentName { get; set; } = "";
    public string MedicationName { get; set; } = "";
    public string Dose { get; set; } = "";
    public string Route { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public TimeOnly ScheduledTime { get; set; }
    public string ApprovalReference { get; set; } = "";
    public string? Instructions { get; set; }
    public string EnteredByName { get; set; } = "";
    public bool IsActive { get; set; }
}

public class CreateMedicationScheduleRequest
{
    [Range(1, int.MaxValue)]
    public int ResidentId { get; set; }

    [Required, MaxLength(200)]
    public string MedicationName { get; set; } = "";

    [Required, MaxLength(100)]
    public string Dose { get; set; } = "";

    [Required, MaxLength(100)]
    public string Route { get; set; } = "";

    [Required]
    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [Required]
    public TimeOnly ScheduledTime { get; set; }

    /// <summary>Số chỉ định/phiếu duyệt. Hệ thống không kê đơn, không đề xuất liều.</summary>
    [Required, MaxLength(200)]
    public string ApprovalReference { get; set; } = "";

    [MaxLength(2000)]
    public string? Instructions { get; set; }
}

/// <summary>Một lần dùng thuốc trong bảng phát thuốc của một ngày.</summary>
public class MedicationDoseDto
{
    public int ScheduleId { get; set; }
    public int ResidentId { get; set; }
    public string ResidentName { get; set; } = "";
    public string ResidentCode { get; set; } = "";
    public string MedicationName { get; set; } = "";
    public string Dose { get; set; } = "";
    public string Route { get; set; } = "";
    public string? Instructions { get; set; }
    public TimeOnly ScheduledTime { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string Status { get; set; } = "Scheduled";
    public DateTime? RecordedAt { get; set; }
    public string? RecordedByName { get; set; }
    public string? Notes { get; set; }

    /// <summary>Đã đến giờ và chưa có bản ghi.</summary>
    public bool CanRecord { get; set; }
}

public class RecordMedicationRequest
{
    [Range(1, int.MaxValue)]
    public int ScheduleId { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    /// <summary>Taken, Missed hoặc Refused. Missed/Refused bắt buộc ghi chú.</summary>
    [Required]
    public string Status { get; set; } = "";

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class MedicationLogDto
{
    public int Id { get; set; }
    public int ScheduleId { get; set; }
    public string MedicationName { get; set; } = "";
    public string Dose { get; set; } = "";
    public DateTime ScheduledAt { get; set; }
    public DateTime? RecordedAt { get; set; }
    public string Status { get; set; } = "";
    public string? RecordedByName { get; set; }
    public string? Notes { get; set; }
}
