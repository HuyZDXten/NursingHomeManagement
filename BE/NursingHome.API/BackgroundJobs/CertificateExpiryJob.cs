using Microsoft.Extensions.Options;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.API.BackgroundJobs;

/// <summary>
/// BL-V4 (UC-43): kiểm tra chứng chỉ nhân viên định kỳ (mặc định 6 giờ). Mỗi (chứng chỉ, mốc) chỉ báo một lần nhờ
/// dấu "đã báo" cập nhật có điều kiện, nên chạy lại hoặc nhiều instance cùng chạy cũng không gửi trùng.
/// Tắt bằng Staff:CertificateJobEnabled = false.
/// </summary>
public sealed class CertificateExpiryJob(
    IServiceScopeFactory scopeFactory,
    IOptions<StaffJobOptions> options,
    ILogger<CertificateExpiryJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.CertificateJobEnabled)
        {
            return;
        }

        // Chạy một lượt ngay sau khi khởi động (chờ API sẵn sàng), rồi theo chu kỳ.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(5, options.Value.CertificateIntervalMinutes)));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<IStaffHrService>().SendExpiryAlertsAsync(stoppingToken);
                if (result.Sent > 0)
                {
                    logger.LogInformation("Cảnh báo chứng chỉ: {Sent} mốc mới / {Checked} chứng chỉ", result.Sent, result.Checked);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Lượt kiểm tra chứng chỉ lỗi, sẽ thử lại ở lượt sau");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
