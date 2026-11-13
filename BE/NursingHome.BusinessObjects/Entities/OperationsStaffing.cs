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

// ---------------- S3-V4 nghỉ phép/đổi ca & bàn giao ca (UC-47, 49). Bảng: 01_Schema.sql mục 9.3 ----------------

public enum LeaveRequestType
{
    /// <summary>Nghỉ từ FromDate đến ToDate; duyệt = bỏ các ca của người xin trong khoảng đó.</summary>
    Leave,

    /// <summary>Đổi một ca của mình lấy một ca của đồng nghiệp cùng role; duyệt = hoán AccountId của 2 ca.</summary>
    Swap
}

public enum LeaveRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}

public class LeaveRequest
{
    public int Id { get; set; }

    public int AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public LeaveRequestType Type { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    /// <summary>Đổi ca: ca của người xin (NULL khi ca đã bị bỏ sau đó).</summary>
    public int? ShiftAssignmentId { get; set; }

    public ShiftAssignment? ShiftAssignment { get; set; }

    public int? SwapWithAccountId { get; set; }

    public Account? SwapWithAccount { get; set; }

    /// <summary>Đổi ca: ca của đồng nghiệp.</summary>
    public int? SwapShiftAssignmentId { get; set; }

    public ShiftAssignment? SwapShiftAssignment { get; set; }

    public string Reason { get; set; } = "";

    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Pending;

    public int? ReviewedById { get; set; }

    public Account? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewNote { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>Bàn giao cuối ca: mỗi người một bàn giao mỗi (ngày, ca). Người có ca kế tiếp (hoặc Admin/Nurse) xác nhận đã đọc.</summary>
public class ShiftHandover
{
    public long Id { get; set; }

    public int ShiftId { get; set; }

    public Shift Shift { get; set; } = null!;

    public DateOnly WorkDate { get; set; }

    public int FromAccountId { get; set; }

    public Account FromAccount { get; set; } = null!;

    public string Summary { get; set; } = "";

    /// <summary>Các mục gợi ý đã chọn, JSON mảng <c>{kind, residentId, residentName, text}</c> (S4-V2 dùng lại).</summary>
    public string? HighlightsJson { get; set; }

    /// <summary>Tóm tắt do AI soạn (S4-V2), người viết đã duyệt.</summary>
    public bool AiGenerated { get; set; }

    public int? AcknowledgedById { get; set; }

    public Account? AcknowledgedBy { get; set; }

    public DateTime? AcknowledgedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
