using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using NursingHome.DTOs.Auth;
using NursingHome.DTOs.Common;

namespace NursingHome.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Null với request ẩn danh (endpoint công khai).</summary>
    public static CurrentUser? ToCurrentUser(this ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        string? role = principal.FindFirstValue(AuthClaimTypes.Role);
        if (role is null || !int.TryParse(principal.FindFirstValue(AuthClaimTypes.Subject), out int accountId))
        {
            return null;
        }

        return new CurrentUser(accountId, role,
            principal.FindFirstValue(AuthClaimTypes.Email) ?? "",
            principal.FindFirstValue(AuthClaimTypes.Name) ?? "");
    }

    public static CurrentUser RequireCurrentUser(this ClaimsPrincipal principal) =>
        principal.ToCurrentUser() ?? throw new InvalidOperationException("Endpoint yêu cầu đăng nhập nhưng không có claim người dùng.");
}

/// <summary>SignalR gửi theo user id = claim "sub" (API tắt MapInboundClaims nên không có NameIdentifier).</summary>
public sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirstValue(AuthClaimTypes.Subject);
}
