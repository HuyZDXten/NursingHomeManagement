using System.Data;
using Microsoft.EntityFrameworkCore;
using NursingHomeManagement.Data;
using NursingHomeManagement.Models;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Services;

public sealed class BillingService(
    ApplicationDbContext database,
    CurrentUser user,
    FacilityClock clock)
{
    public async Task<PagedList<ServicePackage>> PackagesAsync(string? search, int page)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        var query = database.ServicePackages.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(package => package.Name.Contains(search));
        }
        return await PagedList<ServicePackage>.CreateAsync(query.OrderBy(package => package.Name), page);
    }

    public async Task<ServicePackage> PackageAsync(int id)
    {
        await user.RequireRoleAsync(Roles.Admin);
        return await database.ServicePackages.AsNoTracking().SingleOrDefaultAsync(package => package.Id == id)
            ?? throw new KeyNotFoundException();
    }

    public async Task SavePackageAsync(int? id, PackageForm form)
    {
        await user.RequireRoleAsync(Roles.Admin);
        if (form.MonthlyPrice < 0 || decimal.Round(form.MonthlyPrice, 2) != form.MonthlyPrice)
        {
            throw new BusinessException("Giá gói phải không âm và có tối đa 2 chữ số thập phân.");
        }
        var package = id.HasValue
            ? await database.ServicePackages.FindAsync(id.Value) ?? throw new KeyNotFoundException()
            : new ServicePackage();
        package.Name = form.Name.Trim();
        package.MonthlyPrice = form.MonthlyPrice;
        package.Description = form.Description;
        package.IsActive = form.IsActive;
        if (!id.HasValue)
        {
            database.ServicePackages.Add(package);
        }
        await database.SaveChangesAsync();
    }

    public async Task<PagedList<ResidentSubscription>> SubscriptionsAsync(int? residentId, int page)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        var query = database.ResidentSubscriptions.AsNoTracking().Include(item => item.Resident)
            .Include(item => item.ServicePackage).AsQueryable();
        if (residentId.HasValue)
        {
            query = query.Where(item => item.ResidentId == residentId);
        }
        return await PagedList<ResidentSubscription>.CreateAsync(query.OrderByDescending(item => item.StartDate), page);
    }

    public async Task SubscribeAsync(SubscriptionForm form)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (form.EndDate < form.StartDate || !await database.Residents.AnyAsync(resident =>
                resident.Id == form.ResidentId && resident.Status == ResidentStatus.Active))
        {
            throw new BusinessException("Khoảng ngày không hợp lệ hoặc cư dân đã ngừng lưu trú.");
        }
        var package = await database.ServicePackages.SingleOrDefaultAsync(item => item.Id == form.ServicePackageId && item.IsActive)
            ?? throw new BusinessException("Gói dịch vụ không còn hoạt động.");
        if (await database.ResidentSubscriptions.AnyAsync(item => item.ResidentId == form.ResidentId &&
                (form.EndDate == null || item.StartDate <= form.EndDate) && (item.EndDate == null || item.EndDate >= form.StartDate)))
        {
            throw new BusinessException("Cư dân đã có gói trong khoảng thời gian này. Kết thúc gói cũ trước khi đổi gói.");
        }
        database.ResidentSubscriptions.Add(new ResidentSubscription
        {
            ResidentId = form.ResidentId,
            ServicePackageId = form.ServicePackageId,
            StartDate = form.StartDate,
            EndDate = form.EndDate,
            AgreedMonthlyPrice = package.MonthlyPrice
        });
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task EndSubscriptionAsync(int id)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        var subscription = await database.ResidentSubscriptions.FindAsync(id) ?? throw new KeyNotFoundException();
        if (subscription.StartDate > clock.Today)
        {
            throw new BusinessException("Gói chưa bắt đầu. Hãy kiểm tra lại ngày đăng ký.");
        }
        if (subscription.EndDate is null || subscription.EndDate > clock.Today)
        {
            subscription.EndDate = clock.Today;
            await database.SaveChangesAsync();
        }
    }

    public async Task<PagedList<Invoice>> ListAsync(string? search, InvoiceStatus? status, int page)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        var query = database.Invoices.AsNoTracking().Include(invoice => invoice.Resident)
            .Include(invoice => invoice.Items).Include(invoice => invoice.Payments).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(invoice => invoice.Number.Contains(search) || invoice.Resident.FullName.Contains(search));
        }
        if (status.HasValue)
        {
            query = query.Where(invoice => invoice.Status == status);
        }
        return await PagedList<Invoice>.CreateAsync(query.OrderByDescending(invoice => invoice.CreatedAt), page);
    }

    public async Task<Invoice> GetAsync(int id)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        return await database.Invoices.AsNoTracking().Include(invoice => invoice.Resident)
            .Include(invoice => invoice.Items).Include(invoice => invoice.Payments)
            .SingleOrDefaultAsync(invoice => invoice.Id == id) ?? throw new KeyNotFoundException();
    }

    public async Task<int> CreateAsync(InvoiceForm form)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        if (form.BillingPeriod.Day != 1 || form.DueDate < form.BillingPeriod)
        {
            throw new BusinessException("Kỳ hóa đơn phải là ngày đầu tháng và hạn thanh toán không được trước kỳ.");
        }
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var resident = await database.Residents.FindAsync(form.ResidentId) ?? throw new KeyNotFoundException();
        var end = form.BillingPeriod.AddMonths(1).AddDays(-1);
        var subscriptions = await database.ResidentSubscriptions.Include(item => item.ServicePackage)
            .Where(item => item.ResidentId == form.ResidentId && item.StartDate <= end &&
                (item.EndDate == null || item.EndDate >= form.BillingPeriod)).ToListAsync();
        var invoice = new Invoice
        {
            ResidentId = form.ResidentId,
            Number = $"INV-{Guid.NewGuid():N}",
            BillingPeriod = form.BillingPeriod,
            DueDate = form.DueDate
        };
        var admitted = DateOnly.FromDateTime(resident.AdmittedAt.ToOffset(FacilityClock.Offset).DateTime);
        var discharged = resident.DischargedAt.HasValue
            ? DateOnly.FromDateTime(resident.DischargedAt.Value.ToOffset(FacilityClock.Offset).DateTime)
            : DateOnly.MaxValue;
        foreach (var subscription in subscriptions)
        {
            var start = new[] { form.BillingPeriod, subscription.StartDate, admitted }.Max();
            var finish = new[] { end, subscription.EndDate ?? end, discharged }.Min();
            if (finish < start)
            {
                continue;
            }
            int days = finish.DayNumber - start.DayNumber + 1;
            decimal charge = decimal.Round(subscription.AgreedMonthlyPrice * days / end.Day, 2, MidpointRounding.AwayFromZero);
            invoice.Items.Add(new InvoiceItem
            {
                Description = $"{subscription.ServicePackage.Name}: {start:dd/MM}–{finish:dd/MM} ({days} ngày)",
                Quantity = 1,
                UnitPrice = charge
            });
        }
        if (invoice.Items.Count == 0)
        {
            throw new BusinessException("Không có đăng ký gói và ngày lưu trú phù hợp trong kỳ này.");
        }
        database.Invoices.Add(invoice);
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        return invoice.Id;
    }

    public async Task AddItemAsync(int id, InvoiceItemForm form)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var invoice = await database.Invoices.FindAsync(id) ?? throw new KeyNotFoundException();
        if (invoice.Status != InvoiceStatus.Draft || form.Quantity <= 0 || form.UnitPrice < 0 ||
            decimal.Round(form.Quantity, 2) != form.Quantity || decimal.Round(form.UnitPrice, 2) != form.UnitPrice)
        {
            throw new BusinessException("Chỉ thêm phí hợp lệ vào hóa đơn Draft.");
        }
        database.InvoiceItems.Add(new InvoiceItem
        {
            InvoiceId = id,
            Description = form.Description.Trim(),
            Quantity = form.Quantity,
            UnitPrice = form.UnitPrice
        });
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task IssueAsync(int id)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var invoice = await database.Invoices.Include(item => item.Items).SingleOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException();
        if (invoice.Status != InvoiceStatus.Draft || Total(invoice) <= 0)
        {
            throw new BusinessException("Chỉ phát hành hóa đơn Draft có tổng tiền lớn hơn 0.");
        }
        invoice.Status = InvoiceStatus.Unpaid;
        invoice.IssuedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task PayAsync(int id, PaymentForm form)
    {
        await user.RequireRoleAsync(Roles.Admin);
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var invoice = await database.Invoices.Include(item => item.Items).Include(item => item.Payments)
            .SingleOrDefaultAsync(item => item.Id == id) ?? throw new KeyNotFoundException();
        decimal remaining = Total(invoice) - invoice.Payments.Sum(payment => payment.Amount);
        if (invoice.Status is not (InvoiceStatus.Unpaid or InvoiceStatus.PartiallyPaid) ||
            form.Amount <= 0 || form.Amount > remaining || decimal.Round(form.Amount, 2) != form.Amount)
        {
            throw new BusinessException("Số tiền phải lớn hơn 0, không vượt công nợ và có tối đa 2 chữ số thập phân.");
        }
        database.Payments.Add(new Payment
        {
            InvoiceId = id,
            Amount = form.Amount,
            Reference = form.Reference.Trim(),
            RecordedById = user.Id
        });
        invoice.Status = form.Amount == remaining ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task CancelAsync(int id)
    {
        await user.RequireRoleAsync(Roles.Admin);
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var invoice = await database.Invoices.Include(item => item.Payments).SingleOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException();
        if (invoice.Payments.Count > 0 || invoice.Status == InvoiceStatus.Cancelled)
        {
            throw new BusinessException("Không hủy hóa đơn đã có thanh toán hoặc đã hủy.");
        }
        invoice.Status = InvoiceStatus.Cancelled;
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public static decimal Total(Invoice invoice) => invoice.Items.Sum(item => decimal.Round(item.Quantity * item.UnitPrice, 2, MidpointRounding.AwayFromZero));
}
