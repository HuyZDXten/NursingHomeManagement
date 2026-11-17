using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Staffing;

// ===== BL-V4 nhân sự: hồ sơ & chứng chỉ (UC-43), chấm công (UC-48), hiệu suất (UC-126), trang chủ cá nhân (UC-127) =====

/// <summary>State: Valid | ExpiringSoon (còn ≤ 30 ngày) | Expired.</summary>
public class StaffCertificateDto
{
    public int Id { get; set; }

    public int AccountId { get; set; }

    public string AccountName { get; set; } = "";

    public string Name { get; set; } = "";

    public string? Number { get; set; }

    public string? IssuedBy { get; set; }

    public DateOnly? IssuedOn { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    public string State { get; set; } = "";

    /// <summary>Số ngày còn lại (âm = đã quá hạn); null khi không có ngày hết hạn.</summary>
    public int? DaysLeft { get; set; }
}

public class SaveCertificateRequest
{
    [Required(ErrorMessage = "Nhập tên chứng chỉ."), StringLength(200)]
    public string Name { get; set; } = "";

    [StringLength(50)]
    public string? Number { get; set; }

    [StringLength(200)]
    public string? IssuedBy { get; set; }

    public DateOnly? IssuedOn { get; set; }

    public DateOnly? ExpiresOn { get; set; }
}

public class StaffProfileSummaryDto
{
    public int AccountId { get; set; }

    public string FullName { get; set; } = "";

    public string Email { get; set; } = "";

    public string Role { get; set; } = "";

    public bool IsActive { get; set; }

    public string? Position { get; set; }

    public string? Department { get; set; }

    public DateOnly? HireDate { get; set; }

    public int CertificateCount { get; set; }

    public int ExpiringCount { get; set; }

    public int ExpiredCount { get; set; }
}

public class StaffProfileDto
{
    public int AccountId { get; set; }

    public string FullName { get; set; } = "";

    public string Email { get; set; } = "";

    public string? Phone { get; set; }

    public string Role { get; set; } = "";

    public bool IsActive { get; set; }

    public string? Position { get; set; }

    public string? Department { get; set; }

    public DateOnly? HireDate { get; set; }

    public string? IdNumber { get; set; }

    public string? Address { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhone { get; set; }

    public string? Notes { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public List<StaffCertificateDto> Certificates { get; set; } = [];
}

public class SaveStaffProfileRequest
{
    [StringLength(100)]
    public string? Position { get; set; }

    [StringLength(100)]
    public string? Department { get; set; }

    public DateOnly? HireDate { get; set; }

    [StringLength(20)]
    public string? IdNumber { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    [StringLength(150)]
    public string? EmergencyContactName { get; set; }

    [StringLength(30)]
    public string? EmergencyContactPhone { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class CertificateAlertResultDto
{
    /// <summary>Số chứng chỉ được xét (còn ≤ 30 ngày hoặc đã hết hạn).</summary>
    public int Checked { get; set; }

    /// <summary>Số thông báo mốc mới đã gửi (mỗi mốc một lần).</summary>
    public int Sent { get; set; }
}

/// <summary>
/// Status: Upcoming (chưa tới giờ vào ca) | Open (đang trong giờ, chưa vào ca) | InProgress (đã vào, chưa ra) |
/// Done (đã vào và ra) | Missed (ca đã kết thúc mà không chấm công).
/// </summary>
public class MyShiftDto
{
    public int ShiftAssignmentId { get; set; }

    public DateOnly WorkDate { get; set; }

    public string ShiftCode { get; set; } = "";

    public string ShiftName { get; set; } = "";

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public DateTime? CheckInAt { get; set; }

    public DateTime? CheckOutAt { get; set; }

    public int LateMinutes { get; set; }

    public string Status { get; set; } = "";

    public bool CanCheckIn { get; set; }

    public bool CanCheckOut { get; set; }
}

public class CheckAttendanceRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Chọn ca.")]
    public int ShiftAssignmentId { get; set; }
}

/// <summary>Status: OnTime | Late | InProgress | Absent | Upcoming.</summary>
public class AttendanceRecordDto
{
    public int ShiftAssignmentId { get; set; }

    public int AccountId { get; set; }

    public string AccountName { get; set; } = "";

    public DateOnly WorkDate { get; set; }

    public string ShiftName { get; set; } = "";

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public DateTime? CheckInAt { get; set; }

    public DateTime? CheckOutAt { get; set; }

    public int LateMinutes { get; set; }

    public string Status { get; set; } = "";

    public string? EditReason { get; set; }
}

public class EditAttendanceRequest
{
    public DateTime? CheckInAt { get; set; }

    public DateTime? CheckOutAt { get; set; }

    [Required(ErrorMessage = "Nhập lý do sửa công."), StringLength(300)]
    public string Reason { get; set; } = "";
}

public class StaffPerformanceRowDto
{
    public int AccountId { get; set; }

    public string FullName { get; set; } = "";

    public string Role { get; set; } = "";

    /// <summary>Việc chăm sóc có hạn trong khoảng (không tính việc đã hủy).</summary>
    public int TasksDue { get; set; }

    public int TasksOnTime { get; set; }

    public int TasksLate { get; set; }

    /// <summary>Việc quá hạn mà vẫn chưa xong.</summary>
    public int TasksOverdueOpen { get; set; }

    /// <summary>% hoàn thành đúng hạn / tổng việc có hạn; null khi không có việc.</summary>
    public decimal? OnTimeRate { get; set; }

    public int MedicationRecords { get; set; }

    /// <summary>Ca đã kết thúc trong khoảng.</summary>
    public int ShiftsAssigned { get; set; }

    public int ShiftsAttended { get; set; }

    public int LateCount { get; set; }

    public int Absent { get; set; }

    public decimal? AttendanceRate { get; set; }
}

public class StaffPerformanceReportDto
{
    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public List<StaffPerformanceRowDto> Rows { get; set; } = [];
}

public class HomeTaskDto
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public string ResidentCode { get; set; } = "";

    public string ResidentName { get; set; } = "";

    public string Title { get; set; } = "";

    public DateTime DueAt { get; set; }

    /// <summary>Pending | InProgress | Completed.</summary>
    public string Status { get; set; } = "";

    public bool Overdue { get; set; }
}

public class StaffHomeDto
{
    public List<HomeTaskDto> Tasks { get; set; } = [];

    public int TasksDone { get; set; }

    public int TasksOpen { get; set; }

    public int TasksOverdue { get; set; }

    /// <summary>Ca đang trong giờ vào ca hoặc đang làm (để chấm công); null nếu không có.</summary>
    public MyShiftDto? CurrentShift { get; set; }

    public MyShiftDto? NextShift { get; set; }

    public int UnreadHandovers { get; set; }

    public int UnreadMessages { get; set; }

    /// <summary>Chứng chỉ của chính mình còn ≤ 30 ngày hoặc đã hết hạn.</summary>
    public List<StaffCertificateDto> Certificates { get; set; } = [];
}
