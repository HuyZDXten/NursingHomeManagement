using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;

namespace NursingHome.DataAccessObjects;

/// <summary>Giao dịch thanh toán online (PayOS) và nhật ký webhook/đồng bộ.</summary>
public sealed class PaymentDAO : GenericDAO<PaymentTransaction>
{
    public static PaymentDAO Instance { get; } = new();

    private PaymentDAO()
    {
    }

    public async Task<PaymentTransaction?> GetOpenTransactionAsync(int invoiceId)
    {
        await using var context = CreateContext();
        return await context.PaymentTransactions.AsNoTracking()
            .Where(item => item.InvoiceId == invoiceId && item.Status == PaymentTransactionStatus.Pending)
            .OrderByDescending(item => item.Id).FirstOrDefaultAsync();
    }

    public async Task<PaymentTransaction?> GetTransactionAsync(long id)
    {
        await using var context = CreateContext();
        return await context.PaymentTransactions.AsNoTracking()
            .Include(item => item.Invoice).ThenInclude(invoice => invoice.Resident)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task<List<PaymentTransaction>> ListTransactionsAsync(PaymentTransactionStatus? status, int take)
    {
        await using var context = CreateContext();
        var query = context.PaymentTransactions.AsNoTracking()
            .Include(item => item.Invoice).ThenInclude(invoice => invoice.Resident).AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status);
        }

        return await query.OrderByDescending(item => item.Id).Take(take).ToListAsync();
    }

    /// <summary>Giao dịch còn chờ đã có link (cho lớp 2: hỏi trạng thái PayOS).</summary>
    public async Task<List<PaymentTransaction>> ListPendingForSyncAsync(int take)
    {
        await using var context = CreateContext();
        return await context.PaymentTransactions.AsNoTracking()
            .Where(item => item.Status == PaymentTransactionStatus.Pending && item.OrderCode != null)
            .OrderBy(item => item.UpdatedAt ?? item.CreatedAt).Take(take).ToListAsync();
    }

    public async Task SetStatusAsync(long id, PaymentTransactionStatus status, DateTime now)
    {
        await using var context = CreateContext();
        await context.PaymentTransactions
            .Where(item => item.Id == id && item.Status == PaymentTransactionStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, status)
                .SetProperty(item => item.UpdatedAt, now));
    }

    // ---------------- Nhật ký webhook / đồng bộ ----------------

    public Task<PaymentWebhookLog> AddLogAsync(PaymentWebhookLog log) => AddEntityAsync(log);

    public async Task<List<PaymentWebhookLog>> ListLogsAsync(int take)
    {
        await using var context = CreateContext();
        return await context.PaymentWebhookLogs.AsNoTracking().OrderByDescending(log => log.Id).Take(take).ToListAsync();
    }

    public async Task<bool> AppliedEventExistsAsync(string providerEventId)
    {
        await using var context = CreateContext();
        return await context.PaymentWebhookLogs.AnyAsync(log =>
            log.ProviderEventId == providerEventId && log.Outcome == PaymentOutcomes.Applied);
    }
}

public sealed record DashboardStats(
    int ActiveResidents,
    int Rooms,
    int Beds,
    int AvailableBeds,
    int OccupiedBeds,
    int MaintenanceBeds,
    int TasksToday,
    int OpenTasks,
    decimal Billed,
    decimal Collected,
    int UnpaidInvoices,
    int PendingOnlinePayments);

public sealed record MonthlyAmount(int Year, int Month, decimal Amount);

public sealed class DashboardDAO
{
    public static DashboardDAO Instance { get; } = new();

    private DashboardDAO()
    {
    }

    public async Task<DashboardStats> GetStatsAsync(DateTime dayStartUtc, DateTime dayEndUtc)
    {
        await using var context = new BusinessObjects.NursingHomeContext();
        var issued = context.Invoices.Where(invoice => invoice.Status != InvoiceStatus.Draft && invoice.Status != InvoiceStatus.Cancelled)
            .Select(invoice => invoice.Id);
        decimal billed = await context.InvoiceItems.Where(item => issued.Contains(item.InvoiceId))
            .SumAsync(item => (decimal?)Math.Round(item.Quantity * item.UnitPrice, 0)) ?? 0;
        decimal collected = await context.Payments.SumAsync(payment => (decimal?)payment.Amount) ?? 0;

        return new DashboardStats(
            ActiveResidents: await context.Residents.CountAsync(resident => resident.Status == ResidentStatus.Active),
            Rooms: await context.Rooms.CountAsync(room => room.IsActive),
            Beds: await context.Beds.CountAsync(bed => bed.Room.IsActive),
            AvailableBeds: await context.Beds.CountAsync(bed => bed.Status == BedStatus.Available && bed.Room.IsActive),
            OccupiedBeds: await context.Beds.CountAsync(bed => bed.Status == BedStatus.Occupied),
            MaintenanceBeds: await context.Beds.CountAsync(bed => bed.Status == BedStatus.Maintenance),
            TasksToday: await context.CareTasks.CountAsync(task => task.DueAt >= dayStartUtc && task.DueAt < dayEndUtc),
            OpenTasks: await context.CareTasks.CountAsync(task => task.Status == CareTaskStatus.Pending || task.Status == CareTaskStatus.InProgress),
            Billed: billed,
            Collected: collected,
            UnpaidInvoices: await context.Invoices.CountAsync(invoice => invoice.Status == InvoiceStatus.Unpaid || invoice.Status == InvoiceStatus.PartiallyPaid),
            PendingOnlinePayments: await context.PaymentTransactions.CountAsync(item => item.Status == PaymentTransactionStatus.Pending));
    }

    /// <summary>Tiền thực thu theo tháng (giờ cơ sở UTC+7) từ <paramref name="fromUtc"/>.</summary>
    public async Task<List<MonthlyAmount>> CollectedByMonthAsync(DateTime fromUtc)
    {
        await using var context = new BusinessObjects.NursingHomeContext();
        var rows = await context.Payments.Where(payment => payment.PaidAt >= fromUtc)
            .Select(payment => new { Local = payment.PaidAt.AddHours(7), payment.Amount })
            .GroupBy(item => new { item.Local.Year, item.Local.Month })
            .Select(group => new { group.Key.Year, group.Key.Month, Amount = group.Sum(item => item.Amount) })
            .ToListAsync();
        return rows.Select(row => new MonthlyAmount(row.Year, row.Month, row.Amount)).OrderBy(row => row.Year).ThenBy(row => row.Month).ToList();
    }

    public async Task<(int AvailableBeds, int TotalBeds)> GetPublicBedStatsAsync()
    {
        await using var context = new BusinessObjects.NursingHomeContext();
        int total = await context.Beds.CountAsync(bed => bed.Room.IsActive);
        int available = await context.Beds.CountAsync(bed => bed.Status == BedStatus.Available && bed.Room.IsActive);
        return (available, total);
    }
}
