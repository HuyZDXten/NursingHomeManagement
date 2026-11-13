using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Accounts;
using NursingHome.DTOs.Common;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>UC-09..UC-12. Danh sách có lọc/phân trang ở GET /odata/Accounts.</summary>
[ApiController]
[Route("api/accounts")]
[Authorize] // Policy theo action (policy class + action là AND; lookup cần mở cho mọi nhân viên).
public class AccountsController(IAccountService accounts) : ControllerBase
{
    [HttpGet("{id:int}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<AccountDto>> Get(int id) => Ok(await accounts.GetAsync(id));

    [HttpPost]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<AccountDto>> Create([FromBody] CreateAccountRequest request)
    {
        var created = await accounts.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<AccountDto>> Update(int id, [FromBody] UpdateAccountRequest request) =>
        Ok(await accounts.UpdateAsync(User.RequireCurrentUser(), id, request));

    [HttpPost("{id:int}/reset-password")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] AdminResetPasswordRequest request)
    {
        await accounts.ResetPasswordAsync(id, request);
        return NoContent();
    }

    /// <summary>Danh sách chọn nhanh, ví dụ ?roles=Caregiver,Nurse (phân công) hoặc ?roles=Family (liên kết).</summary>
    [HttpGet("lookup")]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<ActionResult<List<LookupItem>>> Lookup([FromQuery] string roles) =>
        Ok(await accounts.LookupAsync(roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
}
