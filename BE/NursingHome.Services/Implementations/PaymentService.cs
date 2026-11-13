using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.BusinessObjects.Helpers;
using NursingHome.DataAccessObjects;
using NursingHome.DTOs.Common;
using NursingHome.DTOs.Payments;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;
using NursingHome.Services.Payments;

namespace NursingHome.Services.Implementations;

/// <summary>
/// UC-93..UC-96: thanh toán hóa đơn bằng PayOS VietQR, theo mô hình StreetBiz:
/// - orderCode = transactionId × 1000 + attempt (mỗi lần tạo link là một mã mới, mã &lt; 1000 là webhook thử).
/// - Xác nhận 2 lớp: webhook (lớp 1) + FE poll / BackgroundService hỏi trạng thái (lớp 2).
/// - Ghi tiền qua BillingDAO.ApplyPaymentAsync: Reference "PAYOS-{orderCode}" là UNIQUE nên không ghi hai lần.
/// Kết quả mỗi báo có được lưu ở PaymentWebhookLogs: APPLIED, DUPLICATE, UNMATCHED, REJECTED, SURPLUS, IGNORED.
/// </summary>
public class PaymentService(
    IPaymentRepository payments,
    IBillingRepository billing,
    IResidentRepository residents,
    IResidentAccessService access,
    INotificationService notifications,
    IPayOsClient payOs,
    IMemoryCache cache,
    IOptions<PayOsOptions> options,
    FacilityClock clock,
    ILogger<PaymentService> logger) : IPaymentService
{
    private const int MaxAttempts = 999;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record StoredCheckout(string? Bin, string? BankName, string? AccountNumber, string? AccountName, string? Description);

    private static readonly Dictionary<string, string> BankNames = new()
    {
        ["970403"] = "Sacombank", ["970405"] = "Agribank", ["970407"] = "Techcombank", ["970415"] = "VietinBank",
        ["970416"] = "ACB", ["970418"] = "BIDV", ["970422"] = "MB Bank", ["970423"] = "TPBank", ["970426"] = "MSB",
        ["970432"] = "VPBank", ["970436"] = "Vietcombank", ["970437"] = "HDBank", ["970441"] = "VIB", ["970443"] = "SHB",
        ["970448"] = "OCB"
    };

    public async Task<CheckoutResponse> CreateCheckoutAsync(CurrentUser user, int invoiceId)
    {
        if (!payOs.IsConfigured)
        {
            throw new ServiceUnavailableException("Thanh toán online chưa được bật. Vui lòng thanh toán tại quầy.");
        }

        var invoice = await billing.GetInvoiceAsync(invoiceId) ?? throw new NotFoundException("Không tìm thấy hóa đơn.");
        if (user.IsInRole(Roles.Family))
        {
            await access.RequireAsync(user, invoice.ResidentId, ResidentSection.Billing);
        }

        if (invoice.Status is not (InvoiceStatus.Unpaid or InvoiceStatus.PartiallyPaid))
        {
            throw new BusinessRuleException("Hóa đơn không ở trạng thái chờ thanh toán.");
        }

        decimal remaining = InvoiceMath.Remaining(invoice);
        var now = clock.UtcNow;
        var open = await payments.GetOpenTransactionAsync(invoiceId);

        // Link cũ còn hạn và đúng số tiền: hiện lại đúng QR đó (tải lại trang không tạo link mới).
        if (open is not null && open.Amount == remaining && open.QrCode is not null && open.ExpiresAt > now.AddMinutes(1))
        {
            return ToCheckout(open, invoice);
        }

        var transaction = open ?? await payments.AddTransactionAsync(new PaymentTransaction
        {
            InvoiceId = invoiceId,
            Provider = "PAYOS",
            Amount = remaining,
            Status = PaymentTransactionStatus.Pending,
            Attempt = 0,
            CreatedById = user.AccountId,
            CreatedAt = now
        });

        int attempt = transaction.Attempt + 1;
        if (attempt > MaxAttempts)
        {
            throw new BusinessRuleException("Đã tạo mã quá nhiều lần cho giao dịch này. Liên hệ kế toán.");
        }

        long orderCode = checked(transaction.Id * 1000 + attempt);
        string description = $"NH{transaction.Id}";
        description = description.Length <= 9 ? description : description[..9];
        var expiresAt = now.AddMinutes(Math.Max(1, options.Value.LinkLifetimeMinutes));
        var link = await payOs.CreatePaymentLinkAsync(orderCode, remaining, description, expiresAt);

        transaction.Attempt = attempt;
        transaction.OrderCode = orderCode;
        transaction.Amount = remaining;
        transaction.CheckoutUrl = link.CheckoutUrl;
        transaction.QrCode = link.QrCode;
        transaction.ProviderReference = link.PaymentLinkId;
        transaction.ExpiresAt = expiresAt;
        transaction.UpdatedAt = now;
        transaction.CheckoutPayload = JsonSerializer.Serialize(new StoredCheckout(
            link.Bin, link.Bin is not null && BankNames.TryGetValue(link.Bin, out var bank) ? bank : null,
            link.AccountNumber, link.AccountName, link.Description), Json);
        await payments.UpdateTransactionAsync(transaction);
        return ToCheckout(transaction, invoice);
    }

    public async Task<PaymentStatusDto> GetStatusAsync(CurrentUser user, long transactionId)
    {
        var transaction = await payments.GetTransactionAsync(transactionId) ?? throw new NotFoundException("Không tìm thấy giao dịch.");
        if (user.IsInRole(Roles.Family))
        {
            await access.RequireAsync(user, transaction.Invoice.ResidentId, ResidentSection.Billing);
        }

        // Lớp 2 khi FE poll: hỏi payOS tối đa mỗi 5 giây cho một giao dịch.
        if (transaction.Status == PaymentTransactionStatus.Pending &&
            !cache.TryGetValue($"payos-sync-{transactionId}", out _))
        {
            cache.Set($"payos-sync-{transactionId}", true, TimeSpan.FromSeconds(5));
            return await SyncAsync(transactionId);
        }

        return ToStatus(transaction);
    }

    public async Task<string> HandleWebhookAsync(string rawPayload)
    {
        var hook = payOs.VerifyWebhook(rawPayload);
        var log = new PaymentWebhookLog
        {
            Source = "Webhook",
            RawPayload = rawPayload,
            SignatureValid = hook.SignatureValid,
            OrderCode = hook.OrderCode,
            Amount = hook.Amount,
            ProviderEventId = hook.Reference,
            ReceivedAt = clock.UtcNow
        };

        if (hook.OrderCode is not { } orderCode || orderCode < 1000)
        {
            return await SaveLogAsync(log, PaymentOutcomes.Ignored, "Webhook thử của payOS hoặc không có orderCode.");
        }

        if (!hook.SignatureValid)
        {
            return await SaveLogAsync(log, PaymentOutcomes.Rejected, "Sai chữ ký webhook.");
        }

        if (!hook.Success || hook.Amount is not { } amount)
        {
            return await SaveLogAsync(log, PaymentOutcomes.Ignored, "Không phải báo có thành công.");
        }

        return await ApplyAsync(orderCode, amount, hook.Reference, log);
    }

    public async Task<PaymentStatusDto> SyncAsync(long transactionId)
    {
        var transaction = await payments.GetTransactionAsync(transactionId) ?? throw new NotFoundException("Không tìm thấy giao dịch.");
        if (transaction.Status == PaymentTransactionStatus.Pending && transaction.OrderCode is { } orderCode)
        {
            var info = await payOs.GetPaymentInfoAsync(orderCode);
            if (info?.Status == "PAID")
            {
                var log = new PaymentWebhookLog
                {
                    Source = "Sync",
                    RawPayload = JsonSerializer.Serialize(info, Json),
                    SignatureValid = true,
                    OrderCode = orderCode,
                    Amount = info.AmountPaid > 0 ? info.AmountPaid : info.Amount,
                    ProviderEventId = info.Reference,
                    ReceivedAt = clock.UtcNow
                };
                await ApplyAsync(orderCode, log.Amount!.Value, info.Reference, log);
            }
            else if (info?.Status is "EXPIRED" or "CANCELLED")
            {
                await payments.SetStatusAsync(transactionId,
                    info.Status == "EXPIRED" ? PaymentTransactionStatus.Expired : PaymentTransactionStatus.Cancelled, clock.UtcNow);
            }

            transaction = await payments.GetTransactionAsync(transactionId) ?? transaction;
        }

        return ToStatus(transaction);
    }

    public async Task<int> SyncPendingAsync(CancellationToken cancellationToken)
    {
        if (!payOs.IsConfigured)
        {
            return 0;
        }

        int checkedCount = 0;
        foreach (var transaction in await payments.ListPendingForSyncAsync(20))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await SyncAsync(transaction.Id);
                checkedCount++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Đồng bộ PayOS thất bại cho giao dịch {TransactionId}", transaction.Id);
            }
        }

        return checkedCount;
    }

    public async Task<List<PaymentTransactionDto>> ListTransactionsAsync(string? status)
    {
        PaymentTransactionStatus? filter = Enum.TryParse<PaymentTransactionStatus>(status, true, out var parsed) ? parsed : null;
        return (await payments.ListTransactionsAsync(filter, 300)).Select(item => item.ToDto()).ToList();
    }

    public async Task<List<WebhookLogDto>> ListLogsAsync() => (await payments.ListLogsAsync(300)).Select(item => item.ToDto()).ToList();

    // ------------------------------------------------------------------

    /// <summary>Áp một báo có (webhook hoặc sync) vào hóa đơn, ghi log và báo cho người liên quan.</summary>
    private async Task<string> ApplyAsync(long orderCode, decimal amount, string? reference, PaymentWebhookLog log)
    {
        var transaction = await payments.GetTransactionAsync(orderCode / 1000);
        if (transaction is null)
        {
            return await SaveLogAsync(log, PaymentOutcomes.Unmatched, "Không có giao dịch nào khớp orderCode.");
        }

        log.PaymentTransactionId = transaction.Id;
        if (!string.IsNullOrWhiteSpace(reference) && await payments.AppliedEventExistsAsync(reference))
        {
            return await SaveLogAsync(log, PaymentOutcomes.Duplicate, "Cùng mã giao dịch ngân hàng đã được ghi.");
        }

        if (amount != transaction.Amount)
        {
            return await SaveLogAsync(log, PaymentOutcomes.Rejected, $"Số tiền {amount:N0} khác số tiền giao dịch {transaction.Amount:N0}.");
        }

        var outcome = await billing.ApplyPaymentAsync(new Payment
        {
            InvoiceId = transaction.InvoiceId,
            Amount = amount,
            Method = PaymentMethod.PayOS,
            Reference = $"PAYOS-{orderCode}",
            RecordedById = null,
            CreatedAt = clock.UtcNow
        }, transaction.Id, clock.UtcNow);

        switch (outcome.Result)
        {
            case PaymentApplyResult.Applied:
                await SaveLogAsync(log, PaymentOutcomes.Applied, null);
                var familyIds = await residents.FamilyAccountIdsAsync(outcome.ResidentId, requireBilling: true);
                string title = $"Đã nhận {amount:N0} đ cho hóa đơn {outcome.InvoiceNumber}";
                await notifications.NotifyAsync(familyIds, NotificationType.Payment, title, "Cảm ơn quý gia đình đã thanh toán qua PayOS.", $"/family/invoices/{transaction.InvoiceId}");
                await notifications.NotifyRolesAsync([Roles.Accountant], NotificationType.Payment, title, null, "/staff/payments");
                return PaymentOutcomes.Applied;
            case PaymentApplyResult.Duplicate:
                return await SaveLogAsync(log, PaymentOutcomes.Duplicate, "Mã tham chiếu đã được ghi.");
            default:
                return await SaveLogAsync(log, PaymentOutcomes.Surplus,
                    "Hóa đơn đã thanh toán/hủy hoặc số tiền vượt công nợ. Cần hoàn tiền thủ công.");
        }
    }

    private async Task<string> SaveLogAsync(PaymentWebhookLog log, string outcome, string? reason)
    {
        log.Outcome = outcome;
        log.Reason = reason;
        await payments.AddLogAsync(log);
        if (outcome is PaymentOutcomes.Unmatched or PaymentOutcomes.Rejected or PaymentOutcomes.Surplus)
        {
            await notifications.NotifyRolesAsync([Roles.Accountant], NotificationType.Payment,
                $"Giao dịch PayOS cần đối soát ({outcome})", reason, "/staff/payments");
        }

        return outcome;
    }

    private static CheckoutResponse ToCheckout(PaymentTransaction transaction, Invoice invoice)
    {
        StoredCheckout? stored = null;
        if (!string.IsNullOrWhiteSpace(transaction.CheckoutPayload))
        {
            try
            {
                stored = JsonSerializer.Deserialize<StoredCheckout>(transaction.CheckoutPayload, Json);
            }
            catch (JsonException)
            {
                stored = null;
            }
        }

        return new CheckoutResponse
        {
            TransactionId = transaction.Id,
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.Number,
            Amount = transaction.Amount,
            Status = transaction.Status.ToString(),
            CheckoutUrl = transaction.CheckoutUrl,
            QrCode = transaction.QrCode,
            BankName = stored?.BankName ?? (stored?.Bin is null ? null : $"Ngân hàng (mã {stored.Bin})"),
            AccountNumber = stored?.AccountNumber,
            AccountName = stored?.AccountName,
            Description = stored?.Description,
            ExpiresAt = transaction.ExpiresAt
        };
    }

    private static PaymentStatusDto ToStatus(PaymentTransaction transaction) => new()
    {
        TransactionId = transaction.Id,
        InvoiceId = transaction.InvoiceId,
        Status = transaction.Status.ToString(),
        InvoiceStatus = transaction.Invoice?.Status.ToString() ?? "",
        Amount = transaction.Amount,
        PaidAt = transaction.PaidAt
    };
}
