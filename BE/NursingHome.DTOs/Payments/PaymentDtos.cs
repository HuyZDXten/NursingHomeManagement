namespace NursingHome.DTOs.Payments;

/// <summary>Những gì màn hình thanh toán cần để vẽ VietQR (không chuyển trang).</summary>
public class CheckoutResponse
{
    public long TransactionId { get; set; }
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public string? CheckoutUrl { get; set; }
    public string? QrCode { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public string? Description { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>Entity set OData "PaymentTransactions" (màn đối soát của Accountant).</summary>
public class PaymentTransactionDto
{
    public long Id { get; set; }
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public string ResidentName { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public long? OrderCode { get; set; }
    public int Attempt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
}

public class PaymentStatusDto
{
    public long TransactionId { get; set; }
    public int InvoiceId { get; set; }
    public string Status { get; set; } = "";
    public string InvoiceStatus { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime? PaidAt { get; set; }
}

public class WebhookLogDto
{
    public long Id { get; set; }
    public string Source { get; set; } = "";
    public bool SignatureValid { get; set; }
    public string Outcome { get; set; } = "";
    public string? Reason { get; set; }
    public long? OrderCode { get; set; }
    public decimal? Amount { get; set; }
    public string? ProviderEventId { get; set; }
    public long? PaymentTransactionId { get; set; }
    public DateTime ReceivedAt { get; set; }
}
