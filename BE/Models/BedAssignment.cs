using System.ComponentModel.DataAnnotations;

namespace NursingHomeManagement.Models;

public class BedAssignment : BaseEntity
{
    public int ResidentId
    {
        get; set;
    }

    public Resident Resident { get; set; } = null!;

    public int BedId
    {
        get; set;
    }

    public Bed Bed { get; set; } = null!;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? EndedAt
    {
        get; set;
    }

    [Required]
    public string AssignedById { get; set; } = "";

    public ApplicationUser AssignedBy { get; set; } = null!;
}
