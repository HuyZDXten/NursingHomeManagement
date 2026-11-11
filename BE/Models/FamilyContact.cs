using System.ComponentModel.DataAnnotations;

namespace NursingHomeManagement.Models;

public class FamilyContact : BaseEntity
{
    public int ResidentId
    {
        get; set;
    }

    public Resident Resident { get; set; } = null!;

    [Required, MaxLength(150)]
    public string FullName { get; set; } = "";

    [Required, MaxLength(50)]
    public string Relationship { get; set; } = "";

    [Required, MaxLength(30)]
    public string Phone { get; set; } = "";

    [EmailAddress, MaxLength(256)]
    public string? Email
    {
        get; set;
    }

    public bool IsEmergencyContact
    {
        get; set;
    }
}
