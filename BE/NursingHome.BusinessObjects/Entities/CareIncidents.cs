namespace NursingHome.BusinessObjects.Entities;

/// <summary>S3-H1: loại sự cố. Lưu NVARCHAR, khớp CK_Incidents_Type.</summary>
public enum IncidentType
{
    Fall,
    Injury,
    Behavior,
    Other
}

/// <summary>S3-H1: mức độ. High báo ngay cho Nurse, Doctor, Admin.</summary>
public enum IncidentSeverity
{
    Low,
    Medium,
    High
}

/// <summary>S3-H1: Open → Investigating → Closed (cho phép Open → Closed). Closed là trạng thái cuối.</summary>
public enum IncidentStatus
{
    Open,
    Investigating,
    Closed
}

/// <summary>S3-H1 (UC-58, UC-59, UC-107): sự cố của cư dân. Bảng dbo.Incidents — giữ tên cột cho báo cáo UC-125.</summary>
public class Incident
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public Resident Resident { get; set; } = null!;

    public IncidentType Type { get; set; }

    public IncidentSeverity Severity { get; set; }

    public DateTime OccurredAt { get; set; }

    public string? Location { get; set; }

    public string Description { get; set; } = "";

    public string? ImmediateAction { get; set; }

    public int ReportedById { get; set; }

    public Account ReportedBy { get; set; } = null!;

    public IncidentStatus Status { get; set; } = IncidentStatus.Open;

    public string? Resolution { get; set; }

    public int? ClosedById { get; set; }

    public Account? ClosedBy { get; set; }

    public DateTime? ClosedAt { get; set; }

    public bool ShareWithFamily { get; set; }

    public DateTime? FamilyNotifiedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
