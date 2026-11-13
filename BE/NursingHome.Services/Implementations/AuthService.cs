using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Auth;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.Services.Implementations;

/// <summary>
/// Phiên JWT như ASS2: access token ngắn hạn + refresh token dùng một lần (xoay vòng).
/// Refresh token đã xoay mà bị dùng lại = bị đánh cắp → thu hồi mọi phiên của tài khoản.
/// Khác ASS2: Admin là một dòng trong bảng Accounts (seed ở 04_ReferenceData.sql), không hardcode.
/// </summary>
public class AuthService(
    IAccountRepository accounts,
    IRefreshTokenRepository refreshTokens,
    IEmailService email,
    PasswordResetOtpStore otps,
    FacilityClock clock,
    IOptions<JwtOptions> jwtOptions,
    ILogger<AuthService> logger) : IAuthService
{
    private const string InvalidCredentials = "Email hoặc mật khẩu không đúng.";
    private const string InvalidSession = "Phiên đăng nhập không còn hiệu lực. Vui lòng đăng nhập lại.";

    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var account = await accounts.GetByEmailAsync(request.Email.Trim());
        if (account is null || !PasswordHasher.Verify(request.Password, account.PasswordHash))
        {
            throw new UnauthorizedServiceException(InvalidCredentials);
        }

        if (!account.IsActive)
        {
            throw new UnauthorizedServiceException("Tài khoản đã bị khóa. Liên hệ quản trị viên.");
        }

        return await IssueTokensAsync(account, RefreshTokenGenerator.Create());
    }

    public async Task<LoginResponse> RefreshAsync(RefreshTokenRequest request)
    {
        var now = clock.UtcNow;
        string hash = RefreshTokenGenerator.Hash(request.RefreshToken);
        var stored = await refreshTokens.GetByHashAsync(hash) ?? throw new UnauthorizedServiceException(InvalidSession);

        if (stored.RevokedAt is not null)
        {
            if (stored.ReplacedByTokenHash is not null)
            {
                await refreshTokens.RevokeAllForAccountAsync(stored.AccountId, now);
            }

            throw new UnauthorizedServiceException(InvalidSession);
        }

        if (stored.ExpiresAt <= now)
        {
            throw new UnauthorizedServiceException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        }

        var account = await accounts.GetByIdAsync(stored.AccountId);
        if (account is null || !account.IsActive)
        {
            await refreshTokens.RevokeAllForAccountAsync(stored.AccountId, now);
            throw new UnauthorizedServiceException(InvalidSession);
        }

        string next = RefreshTokenGenerator.Create();
        if (!await refreshTokens.RevokeAsync(hash, now, RefreshTokenGenerator.Hash(next)))
        {
            throw new UnauthorizedServiceException(InvalidSession);
        }

        return await IssueTokensAsync(account, next);
    }

    public Task LogoutAsync(RefreshTokenRequest request) =>
        refreshTokens.RevokeAsync(RefreshTokenGenerator.Hash(request.RefreshToken), clock.UtcNow, null);

    /// <summary>Trả lời giống nhau dù email có tồn tại hay không (không dò được ai có tài khoản).</summary>
    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        if (!email.IsConfigured)
        {
            throw new ServiceUnavailableException("Chức năng quên mật khẩu chưa được cấu hình email (Email:SmtpUser/SmtpPassword).");
        }

        string address = request.Email.Trim();
        string? code = otps.Issue(address);
        if (code is null)
        {
            throw new TooManyRequestsException("Vui lòng chờ 60 giây trước khi yêu cầu mã mới.");
        }

        var account = await accounts.GetByEmailAsync(address);
        if (account is null || !account.IsActive)
        {
            return;
        }

        string body = $"""
            <p>Xin chào {WebUtility.HtmlEncode(account.FullName)},</p>
            <p>Mã đặt lại mật khẩu của bạn là <b style="font-size:20px">{code}</b> (hiệu lực 10 phút).</p>
            <p>Nếu bạn không yêu cầu, hãy bỏ qua email này.</p>
            """;
        try
        {
            await email.SendAsync(account.Email, "Mã đặt lại mật khẩu", body);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gửi OTP thất bại");
            throw new ServiceUnavailableException("Không gửi được email. Vui lòng thử lại sau.");
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        string address = request.Email.Trim();
        if (!otps.Verify(address, request.Otp))
        {
            throw new BusinessRuleException("Mã không đúng hoặc đã hết hạn. Hãy yêu cầu mã mới.");
        }

        var account = await accounts.GetByEmailAsync(address) ?? throw new BusinessRuleException("Mã không đúng hoặc đã hết hạn.");
        account.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        account.UpdatedAt = clock.UtcNow;
        await accounts.UpdateAsync(account);
        await refreshTokens.RevokeAllForAccountAsync(account.Id, clock.UtcNow);
    }

    public async Task<LoginResponse> ChangePasswordAsync(CurrentUser user, ChangePasswordRequest request)
    {
        var account = await accounts.GetByIdAsync(user.AccountId) ?? throw new UnauthorizedServiceException(InvalidSession);
        if (!PasswordHasher.Verify(request.CurrentPassword, account.PasswordHash))
        {
            throw new BusinessRuleException("Mật khẩu hiện tại không đúng.");
        }

        account.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        account.UpdatedAt = clock.UtcNow;
        await accounts.UpdateAsync(account);

        // Đăng xuất mọi thiết bị khác, cấp phiên mới cho thiết bị đang dùng.
        await refreshTokens.RevokeAllForAccountAsync(account.Id, clock.UtcNow);
        return await IssueTokensAsync(account, RefreshTokenGenerator.Create());
    }

    public async Task<ProfileDto> GetProfileAsync(CurrentUser user)
    {
        var account = await accounts.GetByIdAsync(user.AccountId) ?? throw new NotFoundException("Không tìm thấy tài khoản.");
        return account.ToProfile();
    }

    public async Task<ProfileDto> UpdateProfileAsync(CurrentUser user, UpdateProfileRequest request)
    {
        var account = await accounts.GetByIdAsync(user.AccountId) ?? throw new NotFoundException("Không tìm thấy tài khoản.");
        account.FullName = request.FullName.Trim();
        account.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        account.UpdatedAt = clock.UtcNow;
        await accounts.UpdateAsync(account);
        return account.ToProfile();
    }

    private async Task<LoginResponse> IssueTokensAsync(Account account, string refreshToken)
    {
        var now = clock.UtcNow;
        var refreshExpiresAt = now.AddDays(_jwt.RefreshTokenDays);
        await refreshTokens.AddAsync(new RefreshToken
        {
            TokenHash = RefreshTokenGenerator.Hash(refreshToken),
            AccountId = account.Id,
            CreatedAt = now,
            ExpiresAt = refreshExpiresAt
        });

        var accessExpiresAt = now.AddMinutes(_jwt.AccessTokenMinutes);
        var claims = new[]
        {
            new Claim(AuthClaimTypes.Subject, account.Id.ToString()),
            new Claim(AuthClaimTypes.TokenId, Guid.NewGuid().ToString("N")),
            new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(now).ToString(), ClaimValueTypes.Integer64),
            new Claim(AuthClaimTypes.Name, account.FullName),
            new Claim(AuthClaimTypes.Email, account.Email),
            new Claim(AuthClaimTypes.Role, account.Role)
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims, notBefore: now, expires: accessExpiresAt, signingCredentials: credentials);

        return new LoginResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            AccessTokenExpiresAt = accessExpiresAt,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAt = refreshExpiresAt,
            AccountId = account.Id,
            FullName = account.FullName,
            Email = account.Email,
            Role = account.Role
        };
    }
}
