using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Residents;

/// <summary>Entity set OData "Residents".</summary>
public class ResidentListItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string FullName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public int Age { get; set; }
    public string Gender { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime AdmittedAt { get; set; }
    public DateTime? DischargedAt { get; set; }
    public int? BedId { get; set; }
    public string? BedLabel { get; set; }
}

public class ResidentDetailDto : ResidentListItemDto
{
    public string? Notes { get; set; }
    public string? PhotoUrl { get; set; }
}

public class SaveResidentRequest
{
    [Required, MaxLength(30)]
    public string Code { get; set; } = "";

    [Required, MaxLength(150)]
    public string FullName { get; set; } = "";

    [Required]
    public DateOnly DateOfBirth { get; set; }

    [Required, MaxLength(20)]
    public string Gender { get; set; } = "";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [Url, MaxLength(500)]
    public string? PhotoUrl { get; set; }
}

public class DischargeRequest
{
    [Required, MaxLength(500)]
    public string Reason { get; set; } = "";
}

public class ContactDto
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public string FullName { get; set; } = "";
    public string Relationship { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public bool IsEmergencyContact { get; set; }
}

public class SaveContactRequest
{
    [Required, MaxLength(150)]
    public string FullName { get; set; } = "";

    [Required, MaxLength(50)]
    public string Relationship { get; set; } = "";

    [Required, Phone, MaxLength(30)]
    public string Phone { get; set; } = "";

    [EmailAddress, MaxLength(256)]
    public string? Email { get; set; }

    public bool IsEmergencyContact { get; set; }
}

public class FamilyLinkDto
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public int FamilyAccountId { get; set; }
    public string FamilyName { get; set; } = "";
    public string FamilyEmail { get; set; } = "";
    public bool CanViewHealth { get; set; }
    public bool CanViewCare { get; set; }
    public bool CanViewMedication { get; set; }
    public bool CanViewBilling { get; set; }
}

public class SaveFamilyLinkRequest
{
    [Range(1, int.MaxValue)]
    public int FamilyAccountId { get; set; }

    public bool CanViewHealth { get; set; }
    public bool CanViewCare { get; set; }
    public bool CanViewMedication { get; set; }
    public bool CanViewBilling { get; set; }
}
