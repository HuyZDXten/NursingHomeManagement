using System.Data;
using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.BusinessObjects.Helpers;

namespace NursingHome.DataAccessObjects;

public enum PaymentApplyResult
{
    Applied,
    /// <summary>Mã tham chiếu đã được ghi (payOS gửi lại cùng giao dịch).</summary>
    Duplicate,
    /// <summary>Hóa đơn không ở trạng thái nhận tiền (Draft/Paid/Cancelled).</summary>
    InvalidState,
    /// <summary>Số tiền vượt công nợ còn lại.</summary>
    Overpaid
}

public sealed record PaymentApplyOutcome(PaymentApplyResult Result, int ResidentId, string InvoiceNumber, InvoiceStatus Status, decimal Remaining);

public sealed class BillingDAO : GenericDAO<Invoice>
{
    public static BillingDAO Instance { get; } = new();

    private BillingDAO()
    {
    }

    // ---------------- Gói dịch vụ (UC-83) ----------------

    public async Task<List<ServicePackage>> ListPackagesAsync(bool activeOnly)
    {
        await using var context = CreateContext();
        return await context.ServicePackages.AsNoTracking().Where(package => !activeOnly || package.IsActive)
            .OrderBy(package => package.MonthlyPrice).ToListAsync();
    }

    public async Task<ServicePackage?> GetPackageAsync(int id)
    {
        await using var context = CreateContext();
        return await context.ServicePackages.AsNoTracking().FirstOrDefaultAsync(package => package.Id == id);
    }

    public async Task<bool> PackageNameExistsAsync(string name, int? excludeId)
    {
        await using var context = CreateContext();
        return await context.ServicePackages.AnyAsync(package => package.Name == name && package.Id != excludeId);
    }

    public Task<ServicePackage> AddPackageAsync(ServicePackage package) => AddEntityAsync(package);

    public Task UpdatePackageAsync(ServicePackage package) => UpdateEntityAsync(package);

    // ---------------- Đăng ký gói (UC-84) ----------------

    public async Task<List<ResidentSubscription>> ListSubscriptionsAsync(int? residentId)
    {
        await using var context = CreateContext();
        var query = context.ResidentSubscriptions.AsNoTracking().Include(item => item.Resident).Include(item => item.ServicePackage).AsQueryable();
        if (residentId.HasValue)
        {
            query = query.Where(item => item.ResidentId == residentId);
        }

        return await query.OrderByDescending(item => item.StartDate).ToListAsync();
    }

    /// <summary>Mỗi cư dân tối đa một gói trong cùng khoảng ngày; giá chốt = giá danh mục lúc đăng ký.</summary>
    public async Task<ResidentSubscription> AddSubscriptionAsync(int residentId, int packageId, DateOnly start, DateOnly? end)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (!await context.Residents.AnyAsync(resident => resident.Id == residentId && resident.Status == ResidentStatus.Active))
        {
            throw new BusinessRuleException("Cư dân không tồn tại hoặc đã ngừng lưu trú.");
        }

        var package = await context.ServicePackages.FirstOrDefaultAsync(item => item.Id == packageId && item.IsActive)
            ?? throw new BusinessRuleException("Gói dịch vụ không còn hoạt động.");
        bool overlaps = await context.ResidentSubscriptions.AnyAsync(item => item.ResidentId == residentId &&
            (end == null || item.StartDate <= end) && (item.EndDate == null || item.EndDate >= start));
        if (overlaps)
        {
            throw new BusinessRuleException("Cư dân đã có gói trong khoảng thời gian này. Kết thúc gói cũ trước khi đổi gói.");
        }

        var subscription = new ResidentSubscription
        {
            ResidentId = residentId,
            ServicePackageId = packageId,
            StartDate = start,
            EndDate = end,
            AgreedMonthlyPrice = package.MonthlyPrice
        };
        context.ResidentSubscriptions.Add(subscription);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return subscription;
    }

    public async Task EndSubscriptionAsync(int id, DateOnly today, DateTime now)
    {
        await using var context = CreateContext();
        var subscription = await context.ResidentSubscriptions.FindAsync(id) ?? throw new NotFoundException("Không tìm thấy đăng ký gói.");
        if (subscription.StartDate > today)
        {
            throw new BusinessRuleException("Gói chưa bắt đầu. Hãy kiểm tra lại ngày đăng ký.");
        }

        if (subscription.EndDate is null || subscription.EndDate > today)
        {
            subscription.EndDate = today;
            subscription.UpdatedAt = now;
            await context.SaveChangesAsync();
        }
    }

    public async Task<List<ResidentSubscription>> SubscriptionsForPeriodAsync(int residentId, DateOnly start, DateOnly end)
    {
        await using var context = CreateContext();
        return await context.ResidentSubscriptions.AsNoTracking().Include(item => item.ServicePackage)
            .Where(item => item.ResidentId == residentId && item.StartDate <= end && (item.EndDate == null || item.EndDate >= start))
            .ToListAsync();
    }

    // ---------------- Hóa đơn (UC-87..UC-92) ----------------

    private static IQueryable<Invoice> WithDetails(BusinessObjects.NursingHomeContext context) =>
        context.Invoices.AsNoTracking().Include(invoice => invoice.Resident).Include(invoice => invoice.Items).Include(invoice => invoice.Payments);

    public async Task<List<Invoice>> ListInvoicesAsync(int? residentId, bool excludeDraft)
    {
        await using var context = CreateContext();
        var query = WithDetails(context);
        if (residentId.HasValue)
        {
            query = query.Where(invoice => invoice.ResidentId == residentId);
        }

        if (excludeDraft)
        {
            query = query.Where(invoice => invoice.Status != InvoiceStatus.Draft);
        }

        return await query.OrderByDescending(invoice => invoice.BillingPeriod).ThenByDescending(invoice => invoice.Id).ToListAsync();
    }

    public async Task<Invoice?> GetInvoiceAsync(int id)
    {
        await using var context = CreateContext();
        return await WithDetails(context).FirstOrDefaultAsync(invoice => invoice.Id == id);
    }

    /// <summary>
    /// Tạo hóa đơn trong MỘT transaction Serializable. Unique (ResidentId, BillingPeriod) chặn tạo trùng kỳ (kể cả kỳ đã
    /// Cancelled). S3-V5: thêm mọi lần dùng dịch vụ phát sinh CHƯA vào hóa đơn có UsedOn đến hết kỳ (gồm cả lần ghi muộn
    /// của kỳ trước) thành dòng phí với đơn giá chốt, rồi gán InvoiceItemId — hai kế toán tạo cùng lúc không tính trùng.
    /// </summary>
    public async Task<Invoice> CreateInvoiceAsync(Invoice invoice)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await context.Invoices.AnyAsync(item => item.ResidentId == invoice.ResidentId && item.BillingPeriod == invoice.BillingPeriod))
        {
            throw new ConflictException("Cư dân đã có hóa đơn cho kỳ này.");
        }

        var periodEnd = invoice.BillingPeriod.AddMonths(1).AddDays(-1);
        var usages = await context.ServiceUsages.Include(usage => usage.ExtraService)
            .Where(usage => usage.ResidentId == invoice.ResidentId && usage.InvoiceItemId == null && usage.UsedOn <= periodEnd)
            .OrderBy(usage => usage.UsedOn).ThenBy(usage => usage.Id)
            .ToListAsync();
        var lines = usages.Select(usage => (Usage: usage, Item: new InvoiceItem
        {
            Description = $"{usage.ExtraService.Name} {usage.UsedOn:dd/MM/yyyy} ({usage.ExtraService.Unit})",
            Quantity = usage.Quantity,
            UnitPrice = usage.UnitPrice,
            CreatedAt = invoice.CreatedAt
        })).ToList();
        foreach (var line in lines)
        {
            invoice.Items.Add(line.Item);
        }

        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();
        foreach (var line in lines)
        {
            line.Usage.InvoiceItemId = line.Item.Id;
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return invoice;
    }

    /// <summary>Hóa đơn hàng loạt (S3-V5): cư dân đang ở, hoặc ngừng lưu trú từ <paramref name="dischargedSinceUtc"/>.</summary>
    public async Task<List<Resident>> ListBatchCandidatesAsync(DateTime dischargedSinceUtc)
    {
        await using var context = CreateContext();
        return await context.Residents.AsNoTracking()
            .Where(resident => resident.Status == ResidentStatus.Active || resident.DischargedAt >= dischargedSinceUtc)
            .OrderBy(resident => resident.Code)
            .ToListAsync();
    }

    /// <summary>Số hóa đơn đã có của kỳ, theo cư dân.</summary>
    public async Task<Dictionary<int, string>> InvoiceNumbersForPeriodAsync(DateOnly billingPeriod)
    {
        await using var context = CreateContext();
        return await context.Invoices.AsNoTracking()
            .Where(invoice => invoice.BillingPeriod == billingPeriod)
            .ToDictionaryAsync(invoice => invoice.ResidentId, invoice => invoice.Number);
    }

    public async Task AddItemAsync(int invoiceId, InvoiceItem item, DateTime now)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var invoice = await context.Invoices.FindAsync(invoiceId) ?? throw new NotFoundException("Không tìm thấy hóa đơn.");
        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new BusinessRuleException("Chỉ thêm phí vào hóa đơn Draft.");
        }

        item.InvoiceId = invoiceId;
        context.InvoiceItems.Add(item);
        invoice.UpdatedAt = now;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    /// <summary>Phát hành: khóa các dòng phí, chuyển Unpaid.</summary>
    public async Task<Invoice> IssueAsync(int id, DateTime now)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var invoice = await context.Invoices.Include(item => item.Items).FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new NotFoundException("Không tìm thấy hóa đơn.");
        if (invoice.Status != InvoiceStatus.Draft || InvoiceMath.Total(invoice.Items) <= 0)
        {
            throw new BusinessRuleException("Chỉ phát hành hóa đơn Draft có tổng tiền lớn hơn 0.");
        }

        invoice.Status = InvoiceStatus.Unpaid;
        invoice.IssuedAt = now;
        invoice.UpdatedAt = now;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return invoice;
    }

    public async Task CancelAsync(int id, DateTime now)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var invoice = await context.Invoices.Include(item => item.Payments).FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new NotFoundException("Không tìm thấy hóa đơn.");
        if (invoice.Payments.Count > 0 || invoice.Status == InvoiceStatus.Cancelled)
        {
            throw new BusinessRuleException("Không hủy hóa đơn đã có thanh toán hoặc đã hủy.");
        }

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.UpdatedAt = now;
        // S3-V5: dịch vụ phát sinh của hóa đơn bị hủy được gỡ ra để vào hóa đơn kỳ sau.
        await context.ServiceUsages.Where(usage => usage.InvoiceItem != null && usage.InvoiceItem.InvoiceId == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(usage => usage.InvoiceItemId, (int?)null));
        await context.PaymentTransactions.Where(item => item.InvoiceId == id && item.Status == PaymentTransactionStatus.Pending)
            .ForEachAsync(item =>
            {
                item.Status = PaymentTransactionStatus.Cancelled;
                item.UpdatedAt = now;
            });
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    /// <summary>
    /// Ghi một khoản thu (thủ công hoặc PayOS) trong transaction Serializable: không vượt công nợ,
    /// không ghi hai lần cùng mã tham chiếu (UQ_Payments_Reference). Khi có <paramref name="transactionId"/>
    /// thì đánh dấu giao dịch PayOS là Paid trong cùng transaction. Đủ tiền → hủy các link PayOS còn chờ khác.
    /// </summary>
    public async Task<PaymentApplyOutcome> ApplyPaymentAsync(Payment payment, long? transactionId, DateTime now)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var invoice = await context.Invoices.Include(item => item.Items).Include(item => item.Payments)
            .FirstOrDefaultAsync(item => item.Id == payment.InvoiceId)
            ?? throw new NotFoundException("Không tìm thấy hóa đơn.");
        decimal remaining = InvoiceMath.Remaining(invoice);

        if (await context.Payments.AnyAsync(item => item.Reference == payment.Reference))
        {
            return new PaymentApplyOutcome(PaymentApplyResult.Duplicate, invoice.ResidentId, invoice.Number, invoice.Status, remaining);
        }

        if (invoice.Status is not (InvoiceStatus.Unpaid or InvoiceStatus.PartiallyPaid))
        {
            return new PaymentApplyOutcome(PaymentApplyResult.InvalidState, invoice.ResidentId, invoice.Number, invoice.Status, remaining);
        }

        if (payment.Amount <= 0 || payment.Amount > remaining)
        {
            return new PaymentApplyOutcome(PaymentApplyResult.Overpaid, invoice.ResidentId, invoice.Number, invoice.Status, remaining);
        }

        payment.PaymentTransactionId = transactionId;
        payment.PaidAt = now;
        context.Payments.Add(payment);
        invoice.Status = payment.Amount == remaining ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
        invoice.UpdatedAt = now;

        // Gồm cả giao dịch của khoản tiền này dù đã Expired (webhook đến trễ sau khi link hết hạn).
        var pending = await context.PaymentTransactions
            .Where(item => item.InvoiceId == invoice.Id &&
                (item.Status == PaymentTransactionStatus.Pending || item.Id == transactionId)).ToListAsync();
        foreach (var item in pending)
        {
            if (item.Id == transactionId)
            {
                item.Status = PaymentTransactionStatus.Paid;
                item.PaidAt = now;
                item.UpdatedAt = now;
            }
            else if (invoice.Status == InvoiceStatus.Paid)
            {
                item.Status = PaymentTransactionStatus.Cancelled;
                item.UpdatedAt = now;
            }
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return new PaymentApplyOutcome(PaymentApplyResult.Applied, invoice.ResidentId, invoice.Number, invoice.Status, remaining - payment.Amount);
    }
}
