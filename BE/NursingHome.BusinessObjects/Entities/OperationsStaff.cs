namespace NursingHome.BusinessObjects.Entities;

// MODULE VẬN HÀNH (Viên) — BL-V4 nhân sự (schema mục 9.10).

/// <summary>UC-43: hồ sơ nhân viên, 1–1 với Accounts (nhân viên chưa có dòng thì coi như hồ sơ trống).</summary>
public class StaffProfile
{
    public int AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public string? Position { get; set; }

    public string? Department { get; set; }

    public DateOnly? HireDate { get; set; }

    /// <summary>CCCD/CMND: dữ liệu nhạy cảm, chỉ Admin và chính nhân viên đó xem.</summary>
    public string? IdNumber { get; set; }

    public string? Address { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhone { get; set; }

    public string? Notes { get; set; }

    public DateTime UpdatedAt { get; set; }
}

/// <summary>UC-43: chứng chỉ hành nghề/đào tạo. Alert30At / AlertExpiredAt: đã báo mốc "còn ≤ 30 ngày" / "đã hết hạn".</summary>
public class StaffCertificate
{
    public int Id { get; set; }

    public int AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public string Name { get; set; } = "";

    public string? Number { get; set; }

    public string? IssuedBy { get; set; }

    public DateOnly? IssuedOn { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    public int CreatedById { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? Alert30At { get; set; }

    public DateTime? AlertExpiredAt { get; set; }
}

/// <summary>UC-48: chấm công, mỗi phân ca một dòng. LateMinutes > 0 khi vào ca trễ quá 15 phút.</summary>
public class Attendance
{
    public int Id { get; set; }

    public int ShiftAssignmentId { get; set; }

    public ShiftAssignment ShiftAssignment { get; set; } = null!;

    public int AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public DateTime CheckInAt { get; set; }

    public DateTime? CheckOutAt { get; set; }

    public int LateMinutes { get; set; }

    public string? Note { get; set; }

    public int? EditedById { get; set; }

    public DateTime? EditedAt { get; set; }

    public string? EditReason { get; set; }
}
