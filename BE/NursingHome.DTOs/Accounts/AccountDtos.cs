using System.ComponentModel.DataAnnotations;
using NursingHome.DTOs.Auth;

namespace NursingHome.DTOs.Accounts;

/// <summary>Entity set OData "Accounts".</summary>
public class AccountDto
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string Role { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateAccountRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    [Required, MaxLength(150)]
    public string FullName { get; set; } = "";

    [Phone, MaxLength(30)]
    public string? Phone { get; set; }

    [Required]
    public string Role { get; set; } = "";

    [Required, RegularExpression(PasswordRules.Pattern, ErrorMessage = PasswordRules.Message)]
    public string Password { get; set; } = "";
}

public class UpdateAccountRequest
{
    [Required, MaxLength(150)]
    public string FullName { get; set; } = "";

    [Phone, MaxLength(30)]
    public string? Phone { get; set; }

    [Required]
    public string Role { get; set; } = "";

    public bool IsActive { get; set; } = true;
}

public class AdminResetPasswordRequest
{
    [Required, RegularExpression(PasswordRules.Pattern, ErrorMessage = PasswordRules.Message)]
    public string NewPassword { get; set; } = "";
}
