using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Billing;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>
/// MODULE TÀI CHÍNH (Viên) — S3-V5: UC-85 danh mục dịch vụ phát sinh, UC-86 ghi lần sử dụng, UC-98 hóa đơn hàng loạt,
/// UC-88 hóa đơn PDF, UC-97 nhắc nợ. Policy đặt theo từng action.
/// </summary>
[ApiController]
[Authorize]
public class BillingExtrasController(IExtraServiceService extras, IInvoiceAutomationService automation) : ControllerBase
{
    private const string PdfContentType = "application/pdf";

    // ---------------- Danh mục dịch vụ phát sinh ----------------

    [HttpGet("api/extra-services")]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<ActionResult<List<ExtraServiceDto>>> Services([FromQuery] bool activeOnly = false) =>
        Ok(await extras.ListServicesAsync(activeOnly));

    [HttpPost("api/extra-services")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<ExtraServiceDto>> CreateService([FromBody] SaveExtraServiceRequest request) =>
        Ok(await extras.SaveServiceAsync(null, request));

    [HttpPut("api/extra-services/{id:int}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<ExtraServiceDto>> UpdateService(int id, [FromBody] SaveExtraServiceRequest request) =>
        Ok(await extras.SaveServiceAsync(id, request));

    // ---------------- Lần sử dụng ----------------

    /// <summary>Mọi cư dân người gọi được xem; mặc định từ đầu tháng tới hôm nay.</summary>
    [HttpGet("api/service-usages")]
    [Authorize(Policy = AuthPolicies.ServiceRecorders)]
    public async Task<ActionResult<List<ServiceUsageDto>>> Usages([FromQuery] DateOnly? from, [FromQuery] DateOnly? to) =>
        Ok(await extras.ListUsagesAsync(User.RequireCurrentUser(), null, from, to));

    [HttpGet("api/residents/{residentId:int}/service-usages")]
    [Authorize(Policy = AuthPolicies.ServiceRecorders)]
    public async Task<ActionResult<List<ServiceUsageDto>>> ResidentUsages(int residentId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to) =>
        Ok(await extras.ListUsagesAsync(User.RequireCurrentUser(), residentId, from, to));

    [HttpPost("api/residents/{residentId:int}/service-usages")]
    [Authorize(Policy = AuthPolicies.ServiceRecorders)]
    public async Task<ActionResult<ServiceUsageDto>> RecordUsage(int residentId, [FromBody] CreateServiceUsageRequest request) =>
        Ok(await extras.RecordUsageAsync(User.RequireCurrentUser(), residentId, request));

    /// <summary>Chỉ khi chưa vào hóa đơn.</summary>
    [HttpDelete("api/service-usages/{id:int}")]
    [Authorize(Policy = AuthPolicies.ServiceRecorders)]
    public async Task<IActionResult> DeleteUsage(int id)
    {
        await extras.DeleteUsageAsync(User.RequireCurrentUser(), id);
        return NoContent();
    }

    // ---------------- Hóa đơn: hàng loạt, PDF, nhắc nợ ----------------

    [HttpPost("api/invoices/batch")]
    [Authorize(Policy = AuthPolicies.BillingStaff)]
    public async Task<ActionResult<BatchInvoiceResultDto>> Batch([FromBody] CreateBatchInvoiceRequest request) =>
        Ok(await automation.CreateBatchAsync(request));

    /// <summary>Family chỉ tải được hóa đơn đã phát hành của người thân có quyền Billing.</summary>
    [HttpGet("api/invoices/{id:int}/pdf")]
    [Authorize(Policy = AuthPolicies.InvoiceReaders)]
    public async Task<IActionResult> Pdf(int id)
    {
        var file = await automation.GetPdfAsync(User.RequireCurrentUser(), id);
        return File(file.Content, PdfContentType, file.FileName);
    }

    [HttpGet("api/invoices/{id:int}/reminders")]
    [Authorize(Policy = AuthPolicies.BillingStaff)]
    public async Task<ActionResult<List<InvoiceReminderDto>>> Reminders(int id) => Ok(await automation.ListRemindersAsync(id));

    /// <summary>Chạy nhắc nợ ngay (job nền cũng chạy mỗi giờ). Chạy lại không nhắc trùng mốc.</summary>
    [HttpPost("api/invoices/reminders/run")]
    [Authorize(Policy = AuthPolicies.FinanceStaff)]
    public async Task<ActionResult<ReminderRunResultDto>> RunReminders(CancellationToken cancellationToken) =>
        Ok(await automation.SendRemindersAsync(cancellationToken));
}
