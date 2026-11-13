using NursingHome.BusinessObjects.Enums;

namespace NursingHome.BusinessObjects.Entities;

public class Resident
{
    public int Id { get; set; }

    public string Code { get; set; } = "";

    public string FullName { get; set; } = "";

    public DateOnly DateOfBirth { get; set; }

    public string Gender { get; set; } = "";

    public string? PhotoUrl { get; set; }

    public DateTime AdmittedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DischargedAt { get; set; }

    public ResidentStatus Status { get; set; } = ResidentStatus.Active;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<BedAssignment> BedAssignments { get; set; } = new List<BedAssignment>();
}

public class FamilyContact
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public string FullName { get; set; } = "";

    public string Relationship { get; set; } = "";

    public string Phone { get; set; } = "";

    public string? Email { get; set; }

    public bool IsEmergencyContact { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

public class ResidentFamilyLink
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public Resident Resident { get; set; } = null!;

    public int FamilyAccountId { get; set; }

    public Account FamilyAccount { get; set; } = null!;

    public bool CanViewHealth { get; set; }

    public bool CanViewCare { get; set; }

    public bool CanViewMedication { get; set; }

    public bool CanViewBilling { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
