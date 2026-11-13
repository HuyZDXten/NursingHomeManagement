using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Billing;

// MODULE TÀI CHÍNH (Viên) — S3-V5 dịch vụ phát sinh, hóa đơn hàng loạt, PDF, nhắc nợ (UC-85, 86, 88, 97, 98, 118).

public class ExtraServiceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public bool IsActive { get; set; }
}

public class SaveExtraServiceRequest
{
    [Required(ErrorMessage = "Nhập tên dịch vụ.")]
    [MaxLength(150)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Nhập đơn vị tính.")]
    [MaxLength(30)]
    public string Unit { get; set; } = "";

    [Range(typeof(decimal), "0", "999999999999")]
    public decimal UnitPrice { get; set; }

    public bool IsActive { get; set; } = true;
}

public class ServiceUsageDto
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public string ResidentCode { get; set; } = "";
    public string ResidentName { get; set; } = "";
    public int ExtraServiceId { get; set; }
    public string ServiceName { get; set; } = "";
    public string Unit { get; set; } = "";
    public DateOnly UsedOn { get; set; }
    public decimal Quantity { get; set; }

    /// <summary>Đơn giá chốt lúc ghi.</summary>
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public int RecordedById { get; set; }
    public string RecordedByName { get; set; } = "";

    /// <summary>Đã vào hóa đơn → không xóa được.</summary>
    public bool Invoiced { get; set; }
    public int? InvoiceId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateServiceUsageRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Chọn dịch vụ.")]
    public int ExtraServiceId { get; set; }

    /// <summary>Mặc định hôm nay (giờ cơ sở); không ở tương lai.</summary>
    public DateOnly? UsedOn { get; set; }

    [Range(typeof(decimal), "0.01", "9999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "Số lượng từ 0,01 đến 9999.")]
    public decimal Quantity { get; set; } = 1;

    [MaxLength(500)]
    public string? Note { get; set; }
}

public class CreateBatchInvoiceRequest
{
    /// <summary>Ngày 1 của tháng tính phí.</summary>
    [Required]
    public DateOnly BillingPeriod { get; set; }

    [Required]
    public DateOnly DueDate { get; set; }
}

public class BatchSkipDto
{
    public int ResidentId { get; set; }
    public string ResidentCode { get; set; } = "";
    public string ResidentName { get; set; } = "";
    public string Reason { get; set; } = "";
}

public class BatchInvoiceResultDto
{
    public DateOnly BillingPeriod { get; set; }
    public List<InvoiceListItemDto> Created { get; set; } = [];
    public List<BatchSkipDto> Skipped { get; set; } = [];
}

public class ReminderRunResultDto
{
    /// <summary>Số hóa đơn còn nợ có hạn trong vòng 3 ngày tới hoặc đã quá hạn.</summary>
    public int Checked { get; set; }

    /// <summary>Số lượt nhắc mới (mỗi hóa đơn × mốc chỉ một lần).</summary>
    public int Sent { get; set; }
}

public class InvoiceReminderDto
{
    /// <summary>Before3Days | Overdue | Overdue7.</summary>
    public string Stage { get; set; } = "";
    public DateTime SentAt { get; set; }
}
