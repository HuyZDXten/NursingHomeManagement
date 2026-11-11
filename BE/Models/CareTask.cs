using System.ComponentModel.DataAnnotations;

namespace NursingHomeManagement.Models;

public class CareTask : BaseEntity
{
    public int ResidentId
    {
        get; set;
    }

    public Resident Resident { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(2000)]
    public string? Instructions
    {
        get; set;
    }

    [Required]
    public string AssignedToId { get; set; } = "";

    public ApplicationUser AssignedTo { get; set; } = null!;

    public DateTimeOffset DueAt
    {
        get; set;
    }

    public CareTaskStatus Status
    {
        get; set;
    }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}
