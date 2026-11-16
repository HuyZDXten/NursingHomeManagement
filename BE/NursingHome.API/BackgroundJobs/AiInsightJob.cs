using NursingHome.BusinessObjects.Entities;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Ai;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.BackgroundJobs;

/// <summary>
/// S4-H2: kiểm tra mỗi 15 phút:
/// - Qua 06:00 giờ VN mà hôm nay chưa chạy VitalTrend → phân tích xu hướng sinh hiệu 14 ngày.
/// - Qua 19:00 giờ VN mà hôm nay chưa chạy DailyDigest → tạo nháp nhật ký gửi gia đình.
/// </summary>
public sealed class AiInsightJob(
    IServiceScopeFactory scopeFactory,
    FacilityClock clock,
    ILogger<AiInsightJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            var localNow = clock.UtcNow + FacilityClock.Offset;
            var today = DateOnly.FromDateTime(localNow);

            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAiInsightRepository>();

            if (localNow.Hour >= 6 && !await repo.HasAnyForDateAsync(InsightType.VitalTrend, today))
            {
                int count = await scope.ServiceProvider.GetRequiredService<IVitalTrendService>()
                    .AnalyzeForDateAsync(today, stoppingToken);
                if (count > 0)
                {
                    logger.LogInformation("AiInsightJob đã tạo {Count} cảnh báo xu hướng sinh hiệu ngày {Date}.", count, today);
                }
            }

            if (localNow.Hour >= 19 && !await repo.HasAnyForDateAsync(InsightType.DailyDigest, today))
            {
                int count = await scope.ServiceProvider.GetRequiredService<IDailyDigestService>()
                    .GenerateForDateAsync(today, stoppingToken);
                if (count > 0)
                {
                    logger.LogInformation("AiInsightJob đã tạo {Count} bản nháp nhật ký ngày {Date}.", count, today);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "AiInsightJob lỗi ở lượt chạy hiện tại, sẽ thử lại sau");
        }
    }
}
