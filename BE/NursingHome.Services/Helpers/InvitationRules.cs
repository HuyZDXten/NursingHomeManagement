using System.Security.Cryptography;
using System.Text;
using NursingHome.BusinessObjects.Entities;

namespace NursingHome.Services.Helpers;

/// <summary>BL-V1, UC-08: mã mời một lần, chỉ lưu SHA-256 (hex in hoa, giống CONVERT(CHAR(64), HASHBYTES(...), 2) ở seed).</summary>
public static class InvitationRules
{
    public const int ExpiryDays = 7;

    public const string Pending = "Pending";
    public const string Used = "Used";
    public const string Expired = "Expired";
    public const string Revoked = "Revoked";

    /// <summary>24 ký tự base64url (18 byte ngẫu nhiên) — dán vào link được, không đoán được.</summary>
    public static string NewCode() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim())));

    public static string StatusOf(FamilyInvitation invitation, DateTime now) =>
        invitation.UsedAt is not null ? Used
        : invitation.RevokedAt is not null ? Revoked
        : invitation.ExpiresAt <= now ? Expired
        : Pending;

    public static string Link(string frontendBaseUrl, string code) =>
        $"{frontendBaseUrl.TrimEnd('/')}/kich-hoat?code={Uri.EscapeDataString(code)}";

    /// <summary>Thông báo khi mã không còn dùng được (null = còn hiệu lực).</summary>
    public static string? UnusableReason(string status) => status switch
    {
        Used => "Mã mời đã được dùng. Hãy đăng nhập bằng tài khoản đã tạo.",
        Revoked => "Mã mời đã bị thu hồi. Liên hệ lễ tân để nhận mã mới.",
        Expired => "Mã mời đã hết hạn. Liên hệ lễ tân để nhận mã mới.",
        _ => null
    };
}
