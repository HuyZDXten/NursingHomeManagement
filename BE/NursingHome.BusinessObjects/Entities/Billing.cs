using NursingHome.BusinessObjects.Enums;

namespace NursingHome.BusinessObjects.Entities;

public class ServicePackage
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public decimal MonthlyPrice { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

public class ResidentSubscription
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public Resident Resident { get; set; } = null!;

    public int ServicePackageId { get; set; }

    public ServicePackage ServicePackage { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    /// <summary>Giá chốt lúc đăng ký: đổi giá danh mục không ảnh hưởng.</summary>
    public decimal AgreedMonthlyPrice { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

public class Invoice
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public Resident Resident { get; set; } = null!;

    public string Number { get; set; } = "";

    public DateOnly BillingPeriod { get; set; }

    public DateTime? IssuedAt { get; set; }

    public DateOnly DueDate { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public byte[] RowVersion { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class InvoiceItem
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    public string Description { get; set; } = "";

    public decimal Quantity { get; set; } = 1;

    public decimal UnitPrice { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Payment
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    public string Reference { get; set; } = "";

    /// <summary>Null khi hệ thống tự ghi (webhook PayOS).</summary>
    public int? RecordedById { get; set; }

    public long? PaymentTransactionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PaymentTransaction
{
    public long Id { get; set; }

    public int InvoiceId { get; set; }

    public Invoice Invoice { get; set; } = null!;

    public string Provider { get; set; } = "PAYOS";

    public decimal Amount { get; set; }

    public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.Pending;

    public int Attempt { get; set; }

    public long? OrderCode { get; set; }

    public string? CheckoutUrl { get; set; }

    public string? QrCode { get; set; }

    public string? CheckoutPayload { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public string? ProviderReference { get; set; }

    public int CreatedById { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

public class PaymentWebhookLog
{
    public long Id { get; set; }

    public string Provider { get; set; } = "PAYOS";

    /// <summary>"Webhook" hoặc "Sync" (lớp 2: hỏi trạng thái).</summary>
    public string Source { get; set; } = "Webhook";

    public string RawPayload { get; set; } = "";

    public bool SignatureValid { get; set; }

    /// <summary>Một trong <see cref="Enums.PaymentOutcomes"/>.</summary>
    public string Outcome { get; set; } = "";

    public string? Reason { get; set; }

    public long? OrderCode { get; set; }

    public decimal? Amount { get; set; }

    public string? ProviderEventId { get; set; }

    public long? PaymentTransactionId { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
