using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Staffing;

// MODULE VẬN HÀNH (Viên) — S3-V4 nghỉ phép/đổi ca (UC-47) & bàn giao ca (UC-49).

/// <summary>
/// Type = "Leave": FromDate, ToDate, Reason. Type = "Swap": ShiftAssignmentId (ca của mình), SwapShiftAssignmentId
/// (ca của đồng nghiệp — đồng nghiệp suy ra từ ca), Reason.
/// </summary>
public class CreateLeaveRequest
{
    [Required]
    [RegularExpression("^(Leave|Swap)$", ErrorMessage = "Loại đơn phải là Leave hoặc Swap.")]
    public string Type { get; set; } = "Leave";

    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }

    public int? ShiftAssignmentId { get; set; }

    public int? SwapShiftAssignmentId { get; set; }

    [Required(ErrorMessage = "Nhập lý do.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Lý do 3–500 ký tự.")]
    public string Reason { get; set; } = "";
}

public class ReviewLeaveRequest
{
    [MaxLength(500)]
    public string? Note { get; set; }
}

/// <summary>Một ca trong đơn (NULL khi ca đã bị bỏ khỏi lịch sau khi tạo đơn).</summary>
public class LeaveShiftDto
{
    public int AssignmentId { get; set; }
    public DateOnly WorkDate { get; set; }
    public string ShiftName { get; set; } = "";
    public string ShiftCode { get; set; } = "";
}

public class LeaveRequestDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string AccountName { get; set; } = "";
    public string Role { get; set; } = "";

    /// <summary>Leave | Swap.</summary>
    public string Type { get; set; } = "";
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public LeaveShiftDto? MyShift { get; set; }
    public int? SwapWithAccountId { get; set; }
    public string? SwapWithName { get; set; }
    public LeaveShiftDto? TheirShift { get; set; }
    public string Reason { get; set; } = "";

    /// <summary>Pending | Approved | Rejected | Cancelled.</summary>
    public string Status { get; set; } = "";
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Kết quả duyệt nghỉ phép: các ca đã bỏ (Admin cần xếp người thay).</summary>
public class LeaveApprovalDto
{
    public LeaveRequestDto Request { get; set; } = new();
    public List<LeaveShiftDto> VacatedShifts { get; set; } = [];
}

/// <summary>Một mục gợi ý bàn giao. Kind: Task | Medication | Vital | Incident | Note.</summary>
public class HandoverItemDto
{
    [Required]
    [MaxLength(20)]
    public string Kind { get; set; } = "Note";

    public int? ResidentId { get; set; }

    [MaxLength(200)]
    public string? ResidentName { get; set; }

    [Required]
    [MaxLength(500)]
    public string Text { get; set; } = "";
}

public class HandoverDraftDto
{
    public int ShiftId { get; set; }
    public string ShiftName { get; set; } = "";
    public DateOnly WorkDate { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public List<HandoverItemDto> Items { get; set; } = [];
}

public class CreateHandoverRequest
{
    [Range(1, int.MaxValue)]
    public int ShiftId { get; set; }

    [Required]
    public DateOnly WorkDate { get; set; }

    [Required(ErrorMessage = "Nhập nội dung bàn giao.")]
    [StringLength(4000, MinimumLength = 5, ErrorMessage = "Nội dung bàn giao 5–4000 ký tự.")]
    public string Summary { get; set; } = "";

    [MaxLength(100)]
    public List<HandoverItemDto> Highlights { get; set; } = [];

    /// <summary>Tóm tắt do AI soạn và người viết đã duyệt (S4-V2).</summary>
    public bool AiGenerated { get; set; }
}

public class HandoverDto
{
    public long Id { get; set; }
    public int ShiftId { get; set; }
    public string ShiftCode { get; set; } = "";
    public string ShiftName { get; set; } = "";
    public DateOnly WorkDate { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public int FromAccountId { get; set; }
    public string FromAccountName { get; set; } = "";
    public string Role { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<HandoverItemDto> Highlights { get; set; } = [];
    public bool AiGenerated { get; set; }
    public string? AcknowledgedByName { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Người đang xem được bấm "Đã đọc" (có ca kế tiếp, hoặc Admin/Nurse; không tự xác nhận bàn giao của mình).</summary>
    public bool CanAcknowledge { get; set; }
}

// ---------------- S4-V2 AI tóm tắt bàn giao ca (AI-04) ----------------

public class HandoverAiDraftRequest
{
    [Range(1, int.MaxValue)]
    public int ShiftId { get; set; }

    /// <summary>Ngày bắt đầu ca, mặc định hôm nay.</summary>
    public DateOnly? Date { get; set; }
}

/// <summary>Nháp do AI soạn — FE điền vào ô tóm tắt để người viết sửa rồi mới lưu.</summary>
public class HandoverAiDraftDto
{
    public string Summary { get; set; } = "";

    /// <summary>false khi AI lỗi và đây là bản tóm tắt theo quy tắc (lưu với AiGenerated = false).</summary>
    public bool AiGenerated { get; set; }

    /// <summary>Các mục gợi ý đã dùng (lưu vào HighlightsJson).</summary>
    public List<HandoverItemDto> Items { get; set; } = [];

    public string? Note { get; set; }
}
