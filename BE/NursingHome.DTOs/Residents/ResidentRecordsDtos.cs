using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Residents;

// ===== BL-V2 hồ sơ lưu trú: giấy tờ (UC-29), hợp đồng (UC-30), tạm vắng (UC-31), tái nhập viện (UC-33), tiền cọc (UC-99) =====

public class ResidentDocumentDto
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    /// <summary>IdCard | Insurance | Medical | Other.</summary>
    public string Kind { get; set; } = "";

    public string Title { get; set; } = "";

    public string FileName { get; set; } = "";

    public string ContentType { get; set; } = "";

    public long SizeBytes { get; set; }

    public string? UploadedByName { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class UploadDocumentRequest
{
    [Required(ErrorMessage = "Chọn loại giấy tờ.")]
    [RegularExpression("^(IdCard|Insurance|Medical|Other)$", ErrorMessage = "Loại giấy tờ không hợp lệ.")]
    public string Kind { get; set; } = "";

    [Required(ErrorMessage = "Nhập tên giấy tờ."), StringLength(200)]
    public string Title { get; set; } = "";
}

public class ResidentContractDto
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public string Number { get; set; } = "";

    public DateOnly SignedOn { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public decimal? MonthlyPrice { get; set; }

    public string? Notes { get; set; }

    public bool HasFile { get; set; }

    public string? FileName { get; set; }

    /// <summary>Active | Ended.</summary>
    public string Status { get; set; } = "";

    public string? CreatedByName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? EndedAt { get; set; }
}

public class CreateContractRequest
{
    [Required(ErrorMessage = "Nhập số hợp đồng."), StringLength(30)]
    public string Number { get; set; } = "";

    public DateOnly SignedOn { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [Range(typeof(decimal), "0", "999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Giá tháng không hợp lệ.")]
    public decimal? MonthlyPrice { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class EndContractRequest
{
    /// <summary>Bỏ trống = hôm nay (nếu hợp đồng chưa có ngày kết thúc).</summary>
    public DateOnly? EndDate { get; set; }
}

public class ResidentLeaveDto
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public string ResidentCode { get; set; } = "";

    public string ResidentName { get; set; } = "";

    /// <summary>Home | Hospital | Other.</summary>
    public string Kind { get; set; } = "";

    public DateTime StartAt { get; set; }

    public DateOnly? ExpectedReturnOn { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public string Reason { get; set; } = "";

    public string? CreatedByName { get; set; }

    public bool IsOpen { get; set; }

    /// <summary>Đang vắng và đã quá ngày dự kiến về.</summary>
    public bool Overdue { get; set; }
}

public class StartLeaveRequest
{
    [Required(ErrorMessage = "Chọn loại tạm vắng.")]
    [RegularExpression("^(Home|Hospital|Other)$", ErrorMessage = "Loại tạm vắng không hợp lệ.")]
    public string Kind { get; set; } = "";

    /// <summary>Bỏ trống = bây giờ. Không được ở tương lai.</summary>
    public DateTime? StartAt { get; set; }

    public DateOnly? ExpectedReturnOn { get; set; }

    [Required(ErrorMessage = "Nhập lý do."), StringLength(500)]
    public string Reason { get; set; } = "";
}

public class ReturnLeaveRequest
{
    /// <summary>Bỏ trống = bây giờ.</summary>
    public DateTime? ReturnedAt { get; set; }
}

public class ResidentStayDto
{
    public int Id { get; set; }

    public DateTime AdmittedAt { get; set; }

    public DateTime DischargedAt { get; set; }
}

public class ReadmitRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Chọn giường.")]
    public int BedId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Chọn gói dịch vụ.")]
    public int ServicePackageId { get; set; }

    /// <summary>Bỏ trống = giá niêm yết của gói.</summary>
    [Range(typeof(decimal), "0", "999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Giá thỏa thuận không hợp lệ.")]
    public decimal? AgreedMonthlyPrice { get; set; }

    /// <summary>Ngày bắt đầu tính gói (giờ cơ sở).</summary>
    public DateOnly StartDate { get; set; }
}

public class DepositDto
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public decimal Amount { get; set; }

    /// <summary>Cash | BankTransfer.</summary>
    public string Method { get; set; } = "";

    public string? Reference { get; set; }

    public DateTime ReceivedAt { get; set; }

    public string? ReceivedByName { get; set; }

    public DateTime? RefundedAt { get; set; }

    public decimal? RefundAmount { get; set; }

    public string? RefundNote { get; set; }

    public string? RefundedByName { get; set; }

    public bool Held { get; set; }
}

/// <summary>Tab tài chính: các khoản cọc + số đang giữ + công nợ để kế toán quyết khoản trừ khi hoàn.</summary>
public class DepositSummaryDto
{
    public string ResidentStatus { get; set; } = "";

    public decimal HeldAmount { get; set; }

    public decimal Outstanding { get; set; }

    public List<DepositDto> Deposits { get; set; } = [];
}

public class CreateDepositRequest
{
    [Range(typeof(decimal), "1000", "999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Số tiền cọc từ 1.000 đ.")]
    public decimal Amount { get; set; }

    [Required, RegularExpression("^(Cash|BankTransfer)$", ErrorMessage = "Hình thức thu không hợp lệ.")]
    public string Method { get; set; } = "Cash";

    [StringLength(100)]
    public string? Reference { get; set; }
}

public class RefundDepositRequest
{
    [Range(typeof(decimal), "0", "999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Số tiền hoàn không hợp lệ.")]
    public decimal Amount { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}
