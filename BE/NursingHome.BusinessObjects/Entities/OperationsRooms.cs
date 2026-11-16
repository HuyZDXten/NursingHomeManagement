namespace NursingHome.BusinessObjects.Entities;

// MODULE VẬN HÀNH (Viên) — BL-V3 phòng & giường mở rộng (schema mục 9.9).

/// <summary>UC-36: loại phòng + phụ thu theo ngày (cộng vào hóa đơn theo số ngày ở phòng loại đó).</summary>
public class RoomType
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public decimal DailySurcharge { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public enum ReservationStatus
{
    Active,
    Converted,
    Released
}

/// <summary>UC-41: giữ giường cho khách sắp nhập viện. Active quá hạn được coi là hết hạn.</summary>
public class BedReservation
{
    public int Id { get; set; }

    public int BedId { get; set; }

    public Bed Bed { get; set; } = null!;

    public int? InquiryId { get; set; }

    public Inquiry? Inquiry { get; set; }

    public string HolderName { get; set; } = "";

    public string? Phone { get; set; }

    public string? Note { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Active;

    public int ReservedById { get; set; }

    public Account ReservedBy { get; set; } = null!;

    public DateTime ReservedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public int? ClosedById { get; set; }

    public int? ResidentId { get; set; }
}

public enum MaintenancePriority
{
    Low,
    Normal,
    High
}

public enum MaintenanceStatus
{
    Open,
    InProgress,
    Done,
    Cancelled
}

/// <summary>Luồng phiếu sửa chữa: Open → InProgress → Done; Open/InProgress → Cancelled. Done, Cancelled là trạng thái cuối.</summary>
public static class MaintenanceFlow
{
    public static bool IsFinal(MaintenanceStatus status) => status is MaintenanceStatus.Done or MaintenanceStatus.Cancelled;

    public static bool CanMove(MaintenanceStatus from, MaintenanceStatus to) => (from, to) switch
    {
        (MaintenanceStatus.Open, MaintenanceStatus.InProgress) => true,
        (MaintenanceStatus.Open or MaintenanceStatus.InProgress, MaintenanceStatus.Done or MaintenanceStatus.Cancelled) => true,
        _ => false
    };
}

/// <summary>UC-42: phiếu báo hỏng. BlocksBed: giường trống chuyển sang Maintenance tới khi xử lý xong.</summary>
public class MaintenanceRequest
{
    public int Id { get; set; }

    public int RoomId { get; set; }

    public Room Room { get; set; } = null!;

    public int? BedId { get; set; }

    public Bed? Bed { get; set; }

    public string Title { get; set; } = "";

    public string? Description { get; set; }

    public MaintenancePriority Priority { get; set; } = MaintenancePriority.Normal;

    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Open;

    public bool BlocksBed { get; set; }

    public int ReportedById { get; set; }

    public Account ReportedBy { get; set; } = null!;

    public DateTime ReportedAt { get; set; }

    public string? AssignedTo { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public int? ResolvedById { get; set; }

    public string? ResolutionNote { get; set; }
}
