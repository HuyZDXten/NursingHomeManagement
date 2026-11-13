using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Billing;
using NursingHome.DTOs.Payments;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>UC-83, UC-84: gói dịch vụ và đăng ký gói.</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Staff)]
public class BillingController(IBillingService billing) : ControllerBase
{
    [HttpGet("api/service-packages")]
    public async Task<ActionResult<List<ServicePackageDto>>> Packages([FromQuery] bool activeOnly = false) =>
        Ok(await billing.ListPackagesAsync(activeOnly));

    [HttpPost("api/service-packages")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<ServicePackageDto>> CreatePackage([FromBody] SavePackageRequest request) =>
        Ok(await billing.SavePackageAsync(null, request));

    [HttpPut("api/service-packages/{id:int}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<ServicePackageDto>> UpdatePackage(int id, [FromBody] SavePackageRequest request) =>
        Ok(await billing.SavePackageAsync(id, request));

    [HttpGet("api/subscriptions")]
    [Authorize(Policy = AuthPolicies.BillingStaff)]
    public async Task<ActionResult<List<SubscriptionDto>>> Subscriptions([FromQuery] int? residentId) =>
        Ok(await billing.ListSubscriptionsAsync(residentId));

    [HttpPost("api/subscriptions")]
    [Authorize(Policy = AuthPolicies.BillingStaff)]
    public async Task<ActionResult<SubscriptionDto>> Subscribe([FromBody] CreateSubscriptionRequest request) =>
        Ok(await billing.SubscribeAsync(request));

    [HttpPost("api/subscriptions/{id:int}/end")]
    [Authorize(Policy = AuthPolicies.BillingStaff)]
    public async Task<IActionResult> EndSubscription(int id)
    {
        await billing.EndSubscriptionAsync(id);
        return NoContent();
    }
}

/// <summary>UC-87..UC-93: hóa đơn. Danh sách: GET /odata/Invoices.</summary>
[ApiController]
[Route("api/invoices")]
[Authorize] // Policy đặt theo từng action: policy ở class và action CỘNG DỒN (AND), Family sẽ bị chặn nếu đặt ở class.
public class InvoicesController(IBillingService billing, IPaymentService payments) : ControllerBase
{
    /// <summary>Family xem được hóa đơn đã phát hành của người thân có quyền Billing.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Policy = AuthPolicies.InvoiceReaders)]
    public async Task<ActionResult<InvoiceDetailDto>> Get(int id) => Ok(await billing.GetInvoiceAsync(User.RequireCurrentUser(), id));

    [HttpPost]
    [Authorize(Policy = AuthPolicies.BillingStaff)]
    public async Task<ActionResult<InvoiceDetailDto>> Create([FromBody] CreateInvoiceRequest request) => Ok(await billing.CreateInvoiceAsync(request));

    [HttpPost("{id:int}/items")]
    [Authorize(Policy = AuthPolicies.BillingStaff)]
    public async Task<ActionResult<InvoiceDetailDto>> AddItem(int id, [FromBody] AddInvoiceItemRequest request) => Ok(await billing.AddItemAsync(id, request));

    [HttpPost("{id:int}/issue")]
    [Authorize(Policy = AuthPolicies.BillingStaff)]
    public async Task<ActionResult<InvoiceDetailDto>> Issue(int id) => Ok(await billing.IssueAsync(id));

    [HttpPost("{id:int}/payments")]
    [Authorize(Policy = AuthPolicies.FinanceStaff)]
    public async Task<ActionResult<InvoiceDetailDto>> RecordPayment(int id, [FromBody] RecordPaymentRequest request) =>
        Ok(await billing.RecordPaymentAsync(User.RequireCurrentUser(), id, request));

    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = AuthPolicies.FinanceStaff)]
    public async Task<IActionResult> Cancel(int id)
    {
        await billing.CancelAsync(id);
        return NoContent();
    }

    /// <summary>UC-93/UC-94: tạo (hoặc lấy lại) mã VietQR PayOS cho số tiền còn nợ.</summary>
    [HttpPost("{id:int}/payos/checkout")]
    [Authorize(Policy = AuthPolicies.InvoiceReaders)]
    public async Task<ActionResult<CheckoutResponse>> Checkout(int id) => Ok(await payments.CreateCheckoutAsync(User.RequireCurrentUser(), id));
}
