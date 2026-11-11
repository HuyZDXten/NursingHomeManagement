using System.ComponentModel.DataAnnotations;

namespace NursingHomeManagement.Models;

public class Resident : BaseEntity
{
    [Required, MaxLength(30)]
    public string Code { get; set; } = "";

    [Required, MaxLength(150)]
    public string FullName { get; set; } = "";

    public DateOnly DateOfBirth
    {
        get; set;
    }

    [MaxLength(20)]
    public string Gender { get; set; } = "";

    public DateTimeOffset AdmittedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? DischargedAt
    {
        get; set;
    }

    public ResidentStatus Status { get; set; } = ResidentStatus.Active;

    [MaxLength(2000)]
    public string? Notes
    {
        get; set;
    }
}
