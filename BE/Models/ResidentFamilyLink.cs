using System.ComponentModel.DataAnnotations;

namespace NursingHomeManagement.Models;

public class ResidentFamilyLink : BaseEntity
{
    public int ResidentId
    {
        get; set;
    }

    public Resident Resident { get; set; } = null!;

    [Required]
    public string FamilyUserId { get; set; } = "";

    public ApplicationUser FamilyUser { get; set; } = null!;

    public bool CanViewHealth
    {
        get; set;
    }

    public bool CanViewCare
    {
        get; set;
    }

    public bool CanViewMedication
    {
        get; set;
    }

    public bool CanViewBilling
    {
        get; set;
    }
}
