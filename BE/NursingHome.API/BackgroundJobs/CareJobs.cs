using NursingHome.Services.Interfaces;

namespace NursingHome.API.BackgroundJobs;

/// <summary>S3-H4 (UC-51): mỗi giờ tự động sinh CareTasks cho hôm nay và ngày mai từ CarePlans.</summary>
public sealed class CarePlanJob(
    IServiceScopeFactory scopeFactory,
    ILogger<CarePlanJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<ICarePlanService>().GenerateForDatesAsync(2);
            if (result.CreatedCount > 0)
            {
                logger.LogInformation("CarePlanJob đã sinh {Count} công việc mới.", result.CreatedCount);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "CarePlanJob lỗi ở lượt chạy hiện tại, sẽ thử lại lượt sau");
        }
    }
}

/// <summary>S3-H4 (UC-61): mỗi 5 phút kiểm tra việc sắp đến hạn (30 phút) và quá hạn (60 phút).</summary>
public sealed class TaskReminderJob(
    IServiceScopeFactory scopeFactory,
    ILogger<TaskReminderJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        await RunOnceAsync();

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync();
        }
    }

    private async Task RunOnceAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ITaskReminderService>().RunOnceAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "TaskReminderJob lỗi ở lượt chạy hiện tại, sẽ thử lại lượt sau");
        }
    }
}
