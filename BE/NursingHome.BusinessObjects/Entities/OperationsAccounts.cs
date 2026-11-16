namespace NursingHome.BusinessObjects.Entities;

/// <summary>
/// MODULE VẬN HÀNH (Viên) — BL-V1, UC-08: mã mời kích hoạt tài khoản gia đình (bảng FamilyInvitations, schema mục 9.7).
/// Chỉ lưu SHA-256 của mã; mã gốc chỉ hiện một lần lúc tạo và nằm trong link email.
/// </summary>
public class FamilyInvitation
{
    public int Id { get; set; }

    public string CodeHash { get; set; } = "";

    public string Email { get; set; } = "";

    public int ResidentId { get; set; }

    public Resident Resident { get; set; } = null!;

    public string Relationship { get; set; } = "";

    public bool CanViewHealth { get; set; }

    public bool CanViewCare { get; set; }

    public bool CanViewMedication { get; set; }

    public bool CanViewBilling { get; set; }

    public int CreatedById { get; set; }

    public Account CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public int? AccountId { get; set; }

    public Account? Account { get; set; }

    public DateTime? RevokedAt { get; set; }
}
