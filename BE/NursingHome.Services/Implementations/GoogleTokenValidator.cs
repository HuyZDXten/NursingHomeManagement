using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.Services.Implementations;

/// <summary>BL-V1, UC-02 (chép từ ASS2): kiểm ID token của nút "Sign in with Google".</summary>
public class GoogleTokenValidator(IOptions<GoogleOptions> options, ILogger<GoogleTokenValidator> logger) : IGoogleTokenValidator
{
    private readonly GoogleOptions _options = options.Value;

    public async Task<GoogleIdentity> ValidateAsync(string idToken)
    {
        if (!_options.IsConfigured)
        {
            throw new ServiceUnavailableException("Đăng nhập Google chưa được cấu hình trên máy chủ (Google:ClientId).");
        }

        try
        {
            // Audience = Client ID của mình: token Google cấp cho ứng dụng khác không dùng lại được ở đây.
            // Thư viện kiểm luôn chữ ký, hạn và issuer.
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken, new GoogleJsonWebSignature.ValidationSettings { Audience = [_options.ClientId.Trim()] });
            return new GoogleIdentity(payload.Subject, payload.Email, payload.EmailVerified, payload.Name);
        }
        catch (InvalidJwtException ex)
        {
            logger.LogWarning("Từ chối ID token Google: {Reason}", ex.Message);
            throw new UnauthorizedServiceException("Đăng nhập Google không thành công. Vui lòng thử lại.");
        }
    }
}
