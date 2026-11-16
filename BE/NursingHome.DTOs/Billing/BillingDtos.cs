using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Billing;

public class ServicePackageDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal MonthlyPrice { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class SavePackageRequest
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = "";

    [Range(typeof(decimal), "0", "999999999999")]
    public decimal MonthlyPrice { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class SubscriptionDto
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public string ResidentName { get; set; } = "";
    public int ServicePackageId { get; set; }
    public string PackageName { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal AgreedMonthlyPrice { get; set; }
}

public class CreateSubscriptionRequest
{
    [Range(1, int.MaxValue)]
    public int ResidentId { get; set; }

    [Range(1, int.MaxValue)]
    public int ServicePackageId { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }
}

/// <summary>Entity set OData "Invoices".</summary>
public class InvoiceListItemDto
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public int ResidentId { get; set; }
    public string ResidentName { get; set; } = "";
    public string ResidentCode { get; set; } = "";
    public DateOnly BillingPeriod { get; set; }
    public DateOnly DueDate { get; set; }
    public DateTime? IssuedAt { get; set; }
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
    public decimal Paid { get; set; }
    public decimal Remaining { get; set; }
}

public class InvoiceDetailDto : InvoiceListItemDto
{
    public List<InvoiceItemDto> Items { get; set; } = [];
    public List<PaymentDto> Payments { get; set; } = [];
}

public class InvoiceItemDto
{
    public int Id { get; set; }
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}

public class PaymentDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "";
    public DateTime PaidAt { get; set; }
    public string Reference { get; set; } = "";
}

public class CreateInvoiceRequest
{
    [Range(1, int.MaxValue)]
    public int ResidentId { get; set; }

    /// <summary>Ngày 1 của tháng tính phí.</summary>
    [Required]
    public DateOnly BillingPeriod { get; set; }

    [Required]
    public DateOnly DueDate { get; set; }
}

public class AddInvoiceItemRequest
{
    [Required, MaxLength(250)]
    public string Description { get; set; } = "";

    [Range(typeof(decimal), "0.01", "9999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal Quantity { get; set; } = 1;

    [Range(typeof(decimal), "0", "999999999999")]
    public decimal UnitPrice { get; set; }
}

public class RecordPaymentRequest
{
    [Range(typeof(decimal), "1", "999999999999")]
    public decimal Amount { get; set; }

    /// <summary>Cash hoặc BankTransfer. PayOS chỉ do webhook ghi.</summary>
    [Required]
    public string Method { get; set; } = "Cash";

    [Required, MaxLength(100)]
    public string Reference { get; set; } = "";
}
