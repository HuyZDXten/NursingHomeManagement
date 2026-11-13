using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Payments;
using NursingHome.DTOs.Portal;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>UC-95, UC-96: trạng thái thanh toán, webhook PayOS, đối soát.</summary>
[ApiController]
[Route("api/payments")]
public class PaymentsController(IPaymentService payments, ILogger<PaymentsController> logger) : ControllerBase
{
    /// <summary>FE poll mỗi 5 giây khi đang hiện QR (lớp 2: hỏi payOS nếu còn chờ).</summary>
    [HttpGet("transactions/{id:long}")]
    [Authorize(Policy = AuthPolicies.InvoiceReaders)]
    public async Task<ActionResult<PaymentStatusDto>> Status(long id) => Ok(await payments.GetStatusAsync(User.RequireCurrentUser(), id));

    /// <summary>
    /// Lớp 1: payOS gọi khi có tiền vào. Ẩn danh, xác thực bằng chữ ký ChecksumKey.
    /// Luôn trả 200 để payOS không gửi lại vô hạn; kết quả nằm ở PaymentWebhookLogs.
    /// </summary>
    [HttpPost("payos/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        string raw = await reader.ReadToEndAsync();
        string outcome = await payments.HandleWebhookAsync(raw);
        logger.LogInformation("PayOS webhook: {Outcome}", outcome);
        return Ok(new { error = 0, message = "Ok", data = new { outcome } });
    }

    [HttpPost("transactions/{id:long}/sync")]
    [Authorize(Policy = AuthPolicies.FinanceStaff)]
    public async Task<ActionResult<PaymentStatusDto>> Sync(long id) => Ok(await payments.SyncAsync(id));

    [HttpGet("webhook-logs")]
    [Authorize(Policy = AuthPolicies.FinanceStaff)]
    public async Task<ActionResult<List<WebhookLogDto>>> Logs() => Ok(await payments.ListLogsAsync());
}

/// <summary>UC-100..UC-102: cổng Family.</summary>
[ApiController]
[Route("api/family")]
[Authorize(Policy = AuthPolicies.FamilyOnly)]
public class FamilyController(IFamilyService family) : ControllerBase
{
    [HttpGet("residents")]
    public async Task<ActionResult<List<FamilyResidentDto>>> Residents() => Ok(await family.MyResidentsAsync(User.RequireCurrentUser()));

    [HttpGet("residents/{id:int}")]
    public async Task<ActionResult<FamilyResidentDetailDto>> Resident(int id) => Ok(await family.DetailsAsync(User.RequireCurrentUser(), id));
}

/// <summary>UC-121 dashboard và UC-14..UC-16 trang công khai.</summary>
[ApiController]
public class DashboardController(IDashboardService dashboard) : ControllerBase
{
    [HttpGet("api/dashboard")]
    [Authorize(Policy = AuthPolicies.BillingStaff)]
    public async Task<ActionResult<DashboardDto>> Get() => Ok(await dashboard.GetAsync());

    [HttpGet("api/public/overview")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicOverviewDto>> PublicOverview() => Ok(await dashboard.GetPublicOverviewAsync());
}

/// <summary>UC-117: trung tâm thông báo (realtime qua /hubs/notifications).</summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NotificationSummaryDto>> List() => Ok(await notifications.ListAsync(User.RequireCurrentUser()));

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> Read(long id)
    {
        await notifications.MarkReadAsync(User.RequireCurrentUser(), id);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll()
    {
        await notifications.MarkAllReadAsync(User.RequireCurrentUser());
        return NoContent();
    }
}
