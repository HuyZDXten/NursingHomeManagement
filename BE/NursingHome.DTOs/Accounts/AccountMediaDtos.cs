using System.ComponentModel.DataAnnotations;
using NursingHome.DTOs.Auth;

namespace NursingHome.DTOs.Accounts;

// ===== BL-V1: ảnh (UC-07, UC-24), đăng nhập Google (UC-02), mã mời gia đình (UC-08) =====

/// <summary>Cấu hình công khai cho trang đăng nhập. GoogleClientId rỗng = ẩn nút Google.</summary>
public class AuthConfigDto
{
    public string GoogleClientId { get; set; } = "";
}

public class GoogleLoginRequest
{
    /// <summary>ID token Google trả về cho nút "Sign in with Google".</summary>
    [Required(ErrorMessage = "Thiếu ID token của Google.")]
    [StringLength(4096)]
    public string IdToken { get; set; } = "";
}

public class UploadedImageDto
{
    /// <summary>URL tuyệt đối (Cloudinary) hoặc đường dẫn /uploads/... trên API (lưu local).</summary>
    public string Url { get; set; } = "";
}

public class CreateFamilyInvitationRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Chọn cư dân.")]
    public int ResidentId { get; set; }

    [Required(ErrorMessage = "Nhập email người thân."), EmailAddress(ErrorMessage = "Email không hợp lệ."), StringLength(256)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Nhập quan hệ với cư dân."), StringLength(50)]
    public string Relationship { get; set; } = "";

    public bool CanViewHealth { get; set; } = true;

    public bool CanViewCare { get; set; } = true;

    public bool CanViewMedication { get; set; }

    public bool CanViewBilling { get; set; } = true;
}

/// <summary>Status: Pending | Used | Expired | Revoked.</summary>
public class FamilyInvitationDto
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public string ResidentCode { get; set; } = "";

    public string ResidentName { get; set; } = "";

    public string Email { get; set; } = "";

    public string Relationship { get; set; } = "";

    public bool CanViewHealth { get; set; }

    public bool CanViewCare { get; set; }

    public bool CanViewMedication { get; set; }

    public bool CanViewBilling { get; set; }

    public string Status { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public string? CreatedByName { get; set; }

    public string? AccountName { get; set; }
}

/// <summary>Trả về đúng một lần khi tạo: mã gốc không lưu trong DB nên không xem lại được.</summary>
public class FamilyInvitationCreatedDto
{
    public FamilyInvitationDto Invitation { get; set; } = new();

    public string Code { get; set; } = "";

    public string Link { get; set; } = "";

    public bool EmailSent { get; set; }
}

/// <summary>Trang /kich-hoat hiện thông tin này trước khi người thân đặt mật khẩu.</summary>
public class InvitationLookupDto
{
    public string Email { get; set; } = "";

    public string ResidentName { get; set; } = "";

    public string Relationship { get; set; } = "";

    public DateTime ExpiresAt { get; set; }

    public string FacilityName { get; set; } = "";
}

public class AcceptInvitationRequest
{
    [Required(ErrorMessage = "Thiếu mã mời."), StringLength(100)]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Nhập họ tên."), StringLength(150)]
    public string FullName { get; set; } = "";

    [StringLength(20)]
    public string? Phone { get; set; }

    [Required, RegularExpression(PasswordRules.Pattern, ErrorMessage = PasswordRules.Message)]
    public string Password { get; set; } = "";
}
