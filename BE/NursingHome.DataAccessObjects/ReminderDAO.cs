using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;

namespace NursingHome.DataAccessObjects;

/// <summary>Hóa đơn còn nợ cần xét nhắc, kèm các mốc đã nhắc.</summary>
public sealed record ReminderCandidate(Invoice Invoice, IReadOnlyCollection<ReminderStage> SentStages);

/// <summary>MODULE TÀI CHÍNH (Viên) — S3-V5: nhắc nợ trước/sau hạn (UC-97), mỗi mốc một lần.</summary>
public sealed class ReminderDAO : GenericDAO<InvoiceReminder>
{
    public static ReminderDAO Instance { get; } = new();

    private ReminderDAO()
    {
    }

    /// <summary>Hóa đơn Unpaid/PartiallyPaid có hạn thanh toán trước hoặc bằng <paramref name="dueBy"/>.</summary>
    public async Task<List<ReminderCandidate>> ListCandidatesAsync(DateOnly dueBy)
    {
        await using var context = CreateContext();
        var invoices = await context.Invoices.AsNoTracking()
            .Include(invoice => invoice.Resident)
            .Include(invoice => invoice.Items)
            .Include(invoice => invoice.Payments)
            .Where(invoice => (invoice.Status == InvoiceStatus.Unpaid || invoice.Status == InvoiceStatus.PartiallyPaid) && invoice.DueDate <= dueBy)
            .OrderBy(invoice => invoice.DueDate)
            .ToListAsync();
        var ids = invoices.Select(invoice => invoice.Id).ToList();
        var sent = (await context.InvoiceReminders.AsNoTracking()
                .Where(reminder => ids.Contains(reminder.InvoiceId))
                .Select(reminder => new { reminder.InvoiceId, reminder.Stage })
                .ToListAsync())
            .ToLookup(reminder => reminder.InvoiceId, reminder => reminder.Stage);
        return invoices.Select(invoice => new ReminderCandidate(invoice, sent[invoice.Id].ToList())).ToList();
    }

    /// <summary>
    /// "Giữ chỗ" một mốc nhắc TRƯỚC khi gửi: chèn dòng InvoiceReminders; trùng (job chạy song song, chạy lại) → false
    /// và người gọi không gửi nữa. Nhờ UQ_InvoiceReminders nên mỗi mốc chỉ gửi một lần.
    /// </summary>
    public async Task<bool> TryMarkSentAsync(int invoiceId, ReminderStage stage, DateTime now)
    {
        await using var context = CreateContext();
        context.InvoiceReminders.Add(new InvoiceReminder { InvoiceId = invoiceId, Stage = stage, SentAt = now });
        try
        {
            await context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return false;
        }
    }

    public async Task<List<InvoiceReminder>> ListForInvoiceAsync(int invoiceId)
    {
        await using var context = CreateContext();
        return await context.InvoiceReminders.AsNoTracking().Where(reminder => reminder.InvoiceId == invoiceId)
            .OrderBy(reminder => reminder.SentAt).ToListAsync();
    }
}
