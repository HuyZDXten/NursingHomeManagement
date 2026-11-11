using System.ComponentModel.DataAnnotations;

namespace NursingHomeManagement.Models;

public class MedicationSchedule : BaseEntity
{
    public int ResidentId
    {
        get; set;
    }

    public Resident Resident { get; set; } = null!;

    [Required, MaxLength(200)]
    public string MedicationName { get; set; } = "";

    [Required, MaxLength(100)]
    public string Dose { get; set; } = "";

    [Required, MaxLength(100)]
    public string Route { get; set; } = "";

    public DateOnly StartDate
    {
        get; set;
    }

    public DateOnly? EndDate
    {
        get; set;
    }

    public TimeOnly ScheduledTime
    {
        get; set;
    }

    [Required, MaxLength(200)]
    public string ApprovalReference { get; set; } = "";

    [Required]
    public string EnteredById { get; set; } = "";

    public ApplicationUser EnteredBy { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    [MaxLength(2000)]
    public string? Instructions
    {
        get; set;
    }
}
