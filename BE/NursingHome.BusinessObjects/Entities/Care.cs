using NursingHome.BusinessObjects.Enums;

namespace NursingHome.BusinessObjects.Entities;

public class CareTask
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public Resident Resident { get; set; } = null!;

    public string Title { get; set; } = "";

    public string? Instructions { get; set; }

    public int AssignedToId { get; set; }

    public Account AssignedTo { get; set; } = null!;

    public int CreatedById { get; set; }

    public DateTime DueAt { get; set; }

    public CareTaskStatus Status { get; set; } = CareTaskStatus.Pending;

    public byte[] RowVersion { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

public class CareRecord
{
    public int Id { get; set; }

    public int CareTaskId { get; set; }

    public CareTask CareTask { get; set; } = null!;

    public string Result { get; set; } = "";

    public CareTaskStatus Status { get; set; }

    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

    public int RecordedById { get; set; }

    public Account RecordedBy { get; set; } = null!;

    public bool ShareWithFamily { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class VitalSign
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public int Systolic { get; set; }

    public int Diastolic { get; set; }

    public int HeartRate { get; set; }

    public decimal Temperature { get; set; }

    public decimal? SpO2 { get; set; }

    public string? Notes { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public int RecordedById { get; set; }

    public Account RecordedBy { get; set; } = null!;

    public bool ShareWithFamily { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
