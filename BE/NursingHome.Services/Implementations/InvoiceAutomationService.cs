using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Billing;
using NursingHome.DTOs.Common;
using NursingHome.DTOs.Reports;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.Services.Implementations;

/// <summary>
/// S3-V5 — UC-98 hóa đơn hàng loạt, UC-97 nhắc nợ theo mốc (job mỗi giờ), UC-88 hóa đơn PDF.
/// Tạo từng hóa đơn đi qua <see cref="IBillingService.CreateInvoiceAsync"/> nên quy tắc tính tiền chỉ ở một chỗ.
/// </summary>
public class InvoiceAutomationService(
    IBillingService billingService,
    IBillingRepository billing,
    IReminderRepository reminders,
    IResidentRepository residents,
    INotificationService notifications,
    IInvoiceMailer mailer,
    IOptions<FacilityOptions> facility,
    IOptions<EmailOptions> emailOptions,
    FacilityClock clock,
    ILogger<InvoiceAutomationService> logger) : IInvoiceAutomationService
{
    // ---------------- Hóa đơn hàng loạt ----------------

    /// <summary>Mỗi cư dân tạo riêng: lỗi một người không dừng cả lô, trả lý do bỏ qua.</summary>
    public async Task<BatchInvoiceResultDto> CreateBatchAsync(CreateBatchInvoiceRequest request)
    {
        if (request.BillingPeriod.Day != 1 || request.DueDate < request.BillingPeriod)
        {
            throw new BusinessRuleException("Kỳ hóa đơn phải là ngày đầu tháng và hạn thanh toán không được trước kỳ.");
        }

        var period = request.BillingPeriod;
        var periodEnd = period.AddMonths(1).AddDays(-1);
        var existing = await billing.InvoiceNumbersForPeriodAsync(period);
        var candidates = await billing.ListBatchCandidatesAsync(clock.StartOfDayUtc(period.AddMonths(-2)));
        var result = new BatchInvoiceResultDto { BillingPeriod = period };

        foreach (var resident in candidates)
        {
            var reason = SkipReason(resident, period, periodEnd, existing);
            if (reason is null)
            {
                try
                {
                    result.Created.Add(await billingService.CreateInvoiceAsync(new CreateInvoiceRequest
                    {
                        ResidentId = resident.Id,
                        BillingPeriod = period,
                        DueDate = request.DueDate
                    }));
                    continue;
                }
                catch (Exception ex) when (ex is BusinessRuleException or ConflictException)
                {
                    reason = ex.Message;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Tạo hóa đơn hàng loạt lỗi cho cư dân {ResidentId}", resident.Id);
                    reason = "Lỗi hệ thống khi tạo, hãy thử tạo riêng cho cư dân này.";
                }
            }

            result.Skipped.Add(new BatchSkipDto { ResidentId = resident.Id, ResidentCode = resident.Code, ResidentName = resident.FullName, Reason = reason });
        }

        return result;
    }

    // ---------------- Nhắc nợ ----------------

    /// <summary>
    /// Xét mọi hóa đơn còn nợ có hạn trong 3 ngày tới hoặc đã quá hạn; mỗi (hóa đơn, mốc) chỉ nhắc một lần —
    /// giữ chỗ dòng InvoiceReminders trước rồi mới gửi in-app + email.
    /// </summary>
    public async Task<ReminderRunResultDto> SendRemindersAsync(CancellationToken cancellationToken = default)
    {
        var today = clock.Today;
        var candidates = await reminders.ListCandidatesAsync(today.AddDays(3));
        var result = new ReminderRunResultDto { Checked = candidates.Count };
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (StageFor(candidate.Invoice.DueDate, today) is not { } stage || candidate.SentStages.Contains(stage)
                || !await reminders.TryMarkSentAsync(candidate.Invoice.Id, stage, clock.UtcNow))
            {
                continue;
            }

            var invoice = candidate.Invoice.ToListItem();
            var familyIds = await residents.FamilyAccountIdsAsync(invoice.ResidentId, requireBilling: true);
            await notifications.NotifyAsync(familyIds, NotificationType.Invoice,
                $"Nhắc thanh toán: hóa đơn {invoice.Number} {stage.ToVietnamese()}",
                $"Còn {InvoicePdf.Money(invoice.Remaining)}, hạn {invoice.DueDate:dd/MM/yyyy}.",
                $"/family/invoices/{invoice.Id}");
            if (stage == ReminderStage.Overdue7)
            {
                await notifications.NotifyRolesAsync([Roles.Accountant], NotificationType.Invoice,
                    $"Hóa đơn {invoice.Number} quá hạn trên 7 ngày",
                    $"{invoice.ResidentCode} · {invoice.ResidentName} còn nợ {InvoicePdf.Money(invoice.Remaining)}.",
                    $"/staff/invoices/{invoice.Id}");
            }

            await mailer.SendReminderAsync(invoice, stage);
            result.Sent++;
        }

        return result;
    }

    public async Task<List<InvoiceReminderDto>> ListRemindersAsync(int invoiceId) =>
        (await reminders.ListForInvoiceAsync(invoiceId)).Select(item => item.ToDto()).ToList();

    // ---------------- PDF ----------------

    /// <summary>Quyền như xem hóa đơn: Family chỉ hóa đơn đã phát hành của người thân có quyền Billing.</summary>
    public async Task<ExportFile> GetPdfAsync(CurrentUser user, int id)
    {
        var invoice = await billingService.GetInvoiceAsync(user, id);
        var portal = $"{emailOptions.Value.FrontendBaseUrl.TrimEnd('/')}/family/invoices/{invoice.Id}";
        return new ExportFile(InvoicePdf.Render(invoice, facility.Value, portal), $"{invoice.Number}.pdf");
    }

    // ---------------- Quy tắc thuần (có unit test) ----------------

    /// <summary>Còn 0–3 ngày → Before3Days; quá 1–6 ngày → Overdue; quá từ 7 ngày → Overdue7; còn hơn 3 ngày → chưa nhắc.</summary>
    public static ReminderStage? StageFor(DateOnly dueDate, DateOnly today)
    {
        var daysLeft = dueDate.DayNumber - today.DayNumber;
        return daysLeft switch
        {
            > 3 => null,
            >= 0 => ReminderStage.Before3Days,
            >= -6 => ReminderStage.Overdue,
            _ => ReminderStage.Overdue7
        };
    }

    public static string? SkipReason(Resident resident, DateOnly period, DateOnly periodEnd, IReadOnlyDictionary<int, string> existing)
    {
        if (existing.TryGetValue(resident.Id, out var number))
        {
            return $"Đã có hóa đơn {number} cho kỳ này.";
        }

        if (resident.DischargedAt is { } discharged && FacilityClock.ToLocalDate(discharged) < period)
        {
            return $"Đã ngừng lưu trú từ {FacilityClock.ToLocalDate(discharged):dd/MM/yyyy}.";
        }

        return FacilityClock.ToLocalDate(resident.AdmittedAt) > periodEnd ? "Nhập viện sau kỳ này." : null;
    }
}
