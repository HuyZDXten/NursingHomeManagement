using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Staffing;

// MODULE VẬN HÀNH (Viên) — S3-V2 ca trực & phân công phụ trách (UC-44, 45, 46).

public class ShiftDto
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool CrossesMidnight { get; set; }
}

public class ShiftAssignmentDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string AccountName { get; set; } = "";
    public string Role { get; set; } = "";
    public int ShiftId { get; set; }
    public string ShiftCode { get; set; } = "";
    public string ShiftName { get; set; } = "";
    public DateOnly WorkDate { get; set; }

    /// <summary>Khung giờ thật (UTC) — ca đêm kết thúc ngày hôm sau.</summary>
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string? Note { get; set; }

    /// <summary>Chỉ có khi vừa tạo: cảnh báo làm hai ca liền nhau (vẫn cho xếp).</summary>
    public string? Warning { get; set; }
}

public class CreateShiftAssignmentRequest
{
    [Range(1, int.MaxValue)]
    public int AccountId { get; set; }

    [Range(1, int.MaxValue)]
    public int ShiftId { get; set; }

    [Required]
    public DateOnly WorkDate { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

/// <summary>Hai ngày thứ Hai (yyyy-MM-dd).</summary>
public class CopyWeekRequest
{
    [Required]
    public DateOnly FromWeekStart { get; set; }

    [Required]
    public DateOnly ToWeekStart { get; set; }
}

public class CopyWeekResultDto
{
    public int Copied { get; set; }

    /// <summary>Ô đã có, ngày đã qua hoặc tài khoản đã khóa.</summary>
    public int Skipped { get; set; }
}

public class CareAssignmentDto
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public string ResidentCode { get; set; } = "";
    public string ResidentName { get; set; } = "";
    public int AccountId { get; set; }
    public string AccountName { get; set; } = "";
    public string Role { get; set; } = "";

    /// <summary>NULL = mọi ca.</summary>
    public int? ShiftId { get; set; }
    public string? ShiftName { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; }
    public string AssignedByName { get; set; } = "";
}

public class CreateCareAssignmentRequest
{
    [Range(1, int.MaxValue)]
    public int ResidentId { get; set; }

    [Range(1, int.MaxValue)]
    public int AccountId { get; set; }

    /// <summary>Bỏ trống = phụ trách mọi ca.</summary>
    public int? ShiftId { get; set; }

    /// <summary>Mặc định hôm nay.</summary>
    public DateOnly? StartDate { get; set; }
}
