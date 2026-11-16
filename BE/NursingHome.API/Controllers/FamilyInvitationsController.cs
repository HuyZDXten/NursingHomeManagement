using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Accounts;
using NursingHome.DTOs.Auth;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>BL-V1, UC-08: mã mời kích hoạt tài khoản gia đình. Tạo/xem/thu hồi: lễ tân, Admin. Tra/kích hoạt: công khai.</summary>
[ApiController]
[Route("api/family-invitations")]
[Authorize(Policy = AuthPolicies.FrontDesk)]
public class FamilyInvitationsController(IFamilyInvitationService invitations) : ControllerBase
{
    /// <summary>Trả mã + link MỘT lần (DB chỉ lưu bản băm); EmailSent cho biết đã gửi email chưa.</summary>
    [HttpPost]
    public async Task<ActionResult<FamilyInvitationCreatedDto>> Create([FromBody] CreateFamilyInvitationRequest request) =>
        Ok(await invitations.CreateAsync(User.RequireCurrentUser(), request));

    [HttpGet]
    public async Task<ActionResult<List<FamilyInvitationDto>>> List([FromQuery] int? residentId) => Ok(await invitations.ListAsync(residentId));

    [HttpPost("{id:int}/revoke")]
    public async Task<ActionResult<FamilyInvitationDto>> Revoke(int id) => Ok(await invitations.RevokeAsync(id));

    /// <summary>Trang /kich-hoat: xem mã mời dành cho ai trước khi đặt mật khẩu.</summary>
    [HttpGet("lookup")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    public async Task<ActionResult<InvitationLookupDto>> Lookup([FromQuery] string? code) => Ok(await invitations.LookupAsync(code ?? ""));

    /// <summary>Tạo tài khoản Family + liên kết cư dân, trả phiên đăng nhập.</summary>
    [HttpPost("accept")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    public async Task<ActionResult<LoginResponse>> Accept([FromBody] AcceptInvitationRequest request) => Ok(await invitations.AcceptAsync(request));
}
