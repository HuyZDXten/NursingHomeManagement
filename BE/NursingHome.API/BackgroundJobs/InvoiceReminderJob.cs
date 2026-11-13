using Microsoft.Extensions.Options;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.API.BackgroundJobs;

/// <summary>
/// S3-V5 (UC-97): nhắc nợ định kỳ (mặc định mỗi giờ). Mỗi (hóa đơn, mốc) chỉ nhắc một lần nhờ UQ_InvoiceReminders,
/// nên chạy lại hoặc nhiều instance cùng chạy cũng không gửi trùng. Tắt bằng Billing:ReminderJobEnabled = false.
/// </summary>
public sealed class InvoiceReminderJob(
    IServiceScopeFactory scopeFactory,
    IOptions<BillingJobOptions> options,
    ILogger<InvoiceReminderJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.ReminderJobEnabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(5, options.Value.ReminderIntervalMinutes)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<IInvoiceAutomationService>().SendRemindersAsync(stoppingToken);
                if (result.Sent > 0)
                {
                    logger.LogInformation("Nhắc nợ: {Sent} lượt mới / {Checked} hóa đơn", result.Sent, result.Checked);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Lượt nhắc nợ lỗi, sẽ thử lại ở lượt sau");
            }
        }
    }
}
