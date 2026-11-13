using Microsoft.Extensions.Options;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.API.BackgroundJobs;

/// <summary>
/// Lớp 2 của xác nhận PayOS: định kỳ hỏi payOS trạng thái các giao dịch còn chờ,
/// nên mất webhook (chạy local, mạng chập chờn) vẫn ghi nhận được tiền.
/// </summary>
public sealed class PayOsSyncService(
    IServiceScopeFactory scopeFactory,
    IOptions<PayOsOptions> options,
    ILogger<PayOsSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(15, options.Value.SyncIntervalSeconds));
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IPaymentService>().SyncPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Lượt đồng bộ PayOS lỗi, sẽ thử lại ở lượt sau");
            }
        }
    }
}
