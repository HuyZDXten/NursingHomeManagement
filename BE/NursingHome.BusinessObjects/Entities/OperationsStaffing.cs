namespace NursingHome.BusinessObjects.Entities;

// MODULE VẬN HÀNH & TÀI CHÍNH (Viên) — S3-V2 ca trực & phân công phụ trách (UC-44, 45, 46).
// Bảng: Database/01_Schema.sql mục 9.2 (Shifts, ShiftAssignments, ResidentCareAssignments).

/// <summary>Danh mục ca (04_ReferenceData.sql): MORNING 06–14, AFTERNOON 14–22, NIGHT 22–06 (qua ngày hôm sau).</summary>
public class Shift
{
    public const string MorningCode = "MORNING";
    public const string AfternoonCode = "AFTERNOON";
    public const string NightCode = "NIGHT";

    public int Id { get; set; }

    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>Giờ cơ sở (UTC+7).</summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>Nhỏ hơn hoặc bằng StartTime nghĩa là ca kết thúc vào ngày hôm sau.</summary>
    public TimeOnly EndTime { get; set; }

    public bool IsActive { get; set; } = true;

    public bool CrossesMidnight => EndTime <= StartTime;
}

/// <summary>Một nhân viên làm một ca trong một ngày (WorkDate = ngày bắt đầu ca, giờ cơ sở).</summary>
public class ShiftAssignment
{
    public int Id { get; set; }

    public int AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public int ShiftId { get; set; }

    public Shift Shift { get; set; } = null!;

    public DateOnly WorkDate { get; set; }

    public string? Note { get; set; }

    public int CreatedById { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Nhân viên phụ trách chính một cư dân (ShiftId NULL = mọi ca). Đang hiệu lực khi EndDate IS NULL
/// (UX_RCA_ActivePrimary: mỗi cư dân + ca chỉ một bản ghi đang hiệu lực).
/// </summary>
public class ResidentCareAssignment
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public Resident Resident { get; set; } = null!;

    public int AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public int? ShiftId { get; set; }

    public Shift? Shift { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public int AssignedById { get; set; }

    public Account AssignedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
