using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Accounts;
using NursingHome.DTOs.Billing;
using NursingHome.DTOs.Care;
using NursingHome.DTOs.Payments;
using NursingHome.DTOs.Residents;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

// Các entity set OData (EdmModelBuilder). Service trả danh sách ĐÃ lọc theo quyền người gọi,
// $filter/$orderby/$top/$skip/$count của client chỉ chạy trên tập đó nên không lộ dữ liệu ngoài quyền.
// Ví dụ: GET /odata/Residents?$filter=contains(FullName,'An')&$orderby=Code&$top=10&$skip=0&$count=true

[ApiController]
[Route("odata/Residents")]
[Authorize(Policy = AuthPolicies.Staff)]
public class ResidentsODataController(IResidentService residents) : ODataController
{
    [HttpGet]
    [EnableQuery]
    public async Task<ActionResult<IQueryable<ResidentListItemDto>>> Get() =>
        Ok((await residents.ListAsync(User.RequireCurrentUser())).AsQueryable());
}

[ApiController]
[Route("odata/Accounts")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
public class AccountsODataController(IAccountService accounts) : ODataController
{
    [HttpGet]
    [EnableQuery]
    public async Task<ActionResult<IQueryable<AccountDto>>> Get() => Ok((await accounts.ListAsync()).AsQueryable());
}

[ApiController]
[Route("odata/Invoices")]
[Authorize(Policy = AuthPolicies.BillingStaff)]
public class InvoicesODataController(IBillingService billing) : ODataController
{
    [HttpGet]
    [EnableQuery]
    public async Task<ActionResult<IQueryable<InvoiceListItemDto>>> Get() => Ok((await billing.ListInvoicesAsync(null)).AsQueryable());
}

[ApiController]
[Route("odata/CareTasks")]
[Authorize(Policy = AuthPolicies.ClinicalStaff)]
public class CareTasksODataController(ICareTaskService care) : ODataController
{
    /// <param name="date">Ngày (giờ cơ sở) yyyy-MM-dd; bỏ trống = mọi ngày.</param>
    /// <param name="mine">true = chỉ việc giao cho tôi (Caregiver luôn chỉ thấy việc của mình).</param>
    [HttpGet]
    [EnableQuery]
    public async Task<ActionResult<IQueryable<CareTaskDto>>> Get([FromQuery] DateOnly? date, [FromQuery] bool mine = false, [FromQuery] int? residentId = null) =>
        Ok((await care.ListAsync(User.RequireCurrentUser(), date, mine, residentId)).AsQueryable());
}

[ApiController]
[Route("odata/PaymentTransactions")]
[Authorize(Policy = AuthPolicies.FinanceStaff)]
public class PaymentTransactionsODataController(IPaymentService payments) : ODataController
{
    [HttpGet]
    [EnableQuery]
    public async Task<ActionResult<IQueryable<PaymentTransactionDto>>> Get() => Ok((await payments.ListTransactionsAsync(null)).AsQueryable());
}
