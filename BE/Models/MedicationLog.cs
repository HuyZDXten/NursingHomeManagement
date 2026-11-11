using System.ComponentModel.DataAnnotations;

namespace NursingHomeManagement.Models;

public class MedicationLog : BaseEntity
{
    public int MedicationScheduleId
    {
        get; set;
    }

    public MedicationSchedule MedicationSchedule { get; set; } = null!;

    public DateTimeOffset ScheduledAt
    {
        get; set;
    }

    public DateTimeOffset? RecordedAt
    {
        get; set;
    }

    public MedicationStatus Status
    {
        get; set;
    }

    public string? RecordedById
    {
        get; set;
    }

    public ApplicationUser? RecordedBy
    {
        get; set;
    }

    [MaxLength(2000)]
    public string? Notes
    {
        get; set;
    }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}
