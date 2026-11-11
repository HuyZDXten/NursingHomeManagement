using System.ComponentModel.DataAnnotations;

namespace NursingHomeManagement.Models;

public class VitalSign : BaseEntity
{
    public int ResidentId
    {
        get; set;
    }

    public Resident Resident { get; set; } = null!;

    [Range(40, 300)]
    public int Systolic
    {
        get; set;
    }

    [Range(20, 200)]
    public int Diastolic
    {
        get; set;
    }

    [Range(20, 250)]
    public int HeartRate
    {
        get; set;
    }

    [Range(25, 45)]
    public decimal Temperature
    {
        get; set;
    }

    [Range(0, 100)]
    public decimal? SpO2
    {
        get; set;
    }

    [MaxLength(2000)]
    public string? Notes
    {
        get; set;
    }

    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;

    [Required]
    public string RecordedById { get; set; } = "";

    public ApplicationUser RecordedBy { get; set; } = null!;

    public bool ShareWithFamily
    {
        get; set;
    }
}
