using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Accounts;
using NursingHome.DTOs.Auth;
using NursingHome.DTOs.Common;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>UC-01..UC-07: đăng nhập (cả Google), refresh, đăng xuất, quên/đổi mật khẩu, hồ sơ cá nhân, ảnh đại diện.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth, IMediaService media) : ControllerBase
{
    /// <summary>Tầng HTTP nhận tối đa 10 MB; ảnh hợp lệ tối đa 5 MB (service báo lỗi rõ ràng giữa hai mức).</summary>
    public const long UploadLimit = 10 * 1024 * 1024;

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request) => Ok(await auth.LoginAsync(request));

    /// <summary>BL-V1: cấu hình công khai cho trang đăng nhập (Client ID Google, rỗng = ẩn nút).</summary>
    [HttpGet("config")]
    [AllowAnonymous]
    public ActionResult<AuthConfigDto> Config() => Ok(auth.GetPublicConfig());

    /// <summary>BL-V1, UC-02: đăng nhập bằng ID token Google, chỉ cho tài khoản đã có cùng email.</summary>
    [HttpPost("google")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    public async Task<ActionResult<LoginResponse>> Google([FromBody] GoogleLoginRequest request) => Ok(await auth.GoogleLoginAsync(request));

    /// <summary>BL-V1, UC-07: đổi ảnh đại diện (multipart, trường "file"; JPG/PNG/WebP ≤ 5 MB).</summary>
    [HttpPost("me/avatar")]
    [Authorize]
    [RequestSizeLimit(UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    public async Task<ActionResult<ProfileDto>> UploadAvatar(IFormFile? file)
    {
        await using var content = await file.ReadImageAsync();
        return Ok(await media.SetMyAvatarAsync(User.RequireCurrentUser(), content, file!.FileName, file.Length));
    }

    [HttpDelete("me/avatar")]
    [Authorize]
    public async Task<ActionResult<ProfileDto>> RemoveAvatar() => Ok(await media.RemoveMyAvatarAsync(User.RequireCurrentUser()));

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
