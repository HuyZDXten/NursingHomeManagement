using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Auth;

/// <summary>Tên claim ngắn (API tắt MapInboundClaims), dùng chung cho nơi phát và nơi đọc JWT.</summary>
public static class AuthClaimTypes
{
    public const string Subject = "sub";
    public const string TokenId = "jti";
    public const string Name = "name";
    public const string Email = "email";
    public const string Role = "role";
}

public static class PasswordRules
{
    /// <summary>Tối thiểu 8 ký tự, có chữ hoa, chữ thường, chữ số và ký hiệu.</summary>
    public const string Pattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).{8,100}$";
    public const string Message = "Mật khẩu tối thiểu 8 ký tự, gồm chữ hoa, chữ thường, chữ số và ký hiệu.";
}

public class LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    [Required, MaxLength(100)]
    public string Password { get; set; } = "";
}

public class LoginResponse
{
    public string AccessToken { get; set; } = "";
    public DateTime AccessTokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = "";
    public DateTime RefreshTokenExpiresAt { get; set; }
    public int AccountId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
}

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = "";
}

public class ForgotPasswordRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";
}

public class ResetPasswordRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    [Required, RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP gồm 6 chữ số.")]
    public string Otp { get; set; } = "";

    [Required, RegularExpression(PasswordRules.Pattern, ErrorMessage = PasswordRules.Message)]
    public string NewPassword { get; set; } = "";
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = "";

    [Required, RegularExpression(PasswordRules.Pattern, ErrorMessage = PasswordRules.Message)]
    public string NewPassword { get; set; } = "";
}

public class ProfileDto
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string Role { get; set; } = "";
    public string? AvatarUrl { get; set; }
}

public class UpdateProfileRequest
{
    [Required, MaxLength(150)]
    public string FullName { get; set; } = "";

    [Phone, MaxLength(30)]
    public string? Phone { get; set; }
}
