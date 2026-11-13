using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Auth;
using NursingHome.DTOs.Common;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>UC-01..UC-07: đăng nhập, refresh, đăng xuất, quên/đổi mật khẩu, hồ sơ cá nhân.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request) => Ok(await auth.LoginAsync(request));

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshTokenRequest request) => Ok(await auth.RefreshAsync(request));

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        await auth.LogoutAsync(request);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    public async Task<ActionResult<MessageResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await auth.ForgotPasswordAsync(request);
        return Ok(new MessageResponse("Nếu email thuộc một tài khoản đang hoạt động, mã đặt lại mật khẩu đã được gửi."));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    public async Task<ActionResult<MessageResponse>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        await auth.ResetPasswordAsync(request);
        return Ok(new MessageResponse("Đã đặt lại mật khẩu. Vui lòng đăng nhập lại."));
    }

    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    public async Task<ActionResult<LoginResponse>> ChangePassword([FromBody] ChangePasswordRequest request) =>
        Ok(await auth.ChangePasswordAsync(User.RequireCurrentUser(), request));

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ProfileDto>> Me() => Ok(await auth.GetProfileAsync(User.RequireCurrentUser()));

    [HttpPut("me")]
    [Authorize]
    public async Task<ActionResult<ProfileDto>> UpdateMe([FromBody] UpdateProfileRequest request) =>
        Ok(await auth.UpdateProfileAsync(User.RequireCurrentUser(), request));
}
