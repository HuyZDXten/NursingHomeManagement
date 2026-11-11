using System.ComponentModel.DataAnnotations;

namespace NursingHomeManagement.Models;

public class CareRecord : BaseEntity
{
    public int CareTaskId
    {
        get; set;
    }

    public CareTask CareTask { get; set; } = null!;

    [Required, MaxLength(2000)]
    public string Result { get; set; } = "";

    public CareTaskStatus Status
    {
        get; set;
    }

    public DateTimeOffset PerformedAt { get; set; } = DateTimeOffset.UtcNow;

    [Required]
    public string RecordedById { get; set; } = "";

    public ApplicationUser RecordedBy { get; set; } = null!;

    public bool ShareWithFamily
    {
        get; set;
    }
}
