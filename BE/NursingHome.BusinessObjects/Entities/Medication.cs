using NursingHome.BusinessObjects.Enums;

namespace NursingHome.BusinessObjects.Entities;

public class MedicationSchedule
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public Resident Resident { get; set; } = null!;

    public string MedicationName { get; set; } = "";

    public string Dose { get; set; } = "";

    public string Route { get; set; } = "";

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public TimeOnly ScheduledTime { get; set; }

    public string ApprovalReference { get; set; } = "";

    public string? Instructions { get; set; }

    public int EnteredById { get; set; }

    public Account EnteredBy { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

public class MedicationLog
{
    public int Id { get; set; }

    public int MedicationScheduleId { get; set; }

    public MedicationSchedule MedicationSchedule { get; set; } = null!;

    public DateTime ScheduledAt { get; set; }

    public DateTime? RecordedAt { get; set; }

    public MedicationStatus Status { get; set; }

    public int? RecordedById { get; set; }

    public Account? RecordedBy { get; set; }

    public string? Notes { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
