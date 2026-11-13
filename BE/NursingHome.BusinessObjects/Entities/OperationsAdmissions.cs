namespace NursingHome.BusinessObjects.Entities;

// MODULE VẬN HÀNH & TÀI CHÍNH (Viên) — S3-V1 tiếp nhận & tham quan (UC-17, 18, 21, 22).
// Bảng: Database/01_Schema.sql mục 9 (Inquiries, TourBookings).

/// <summary>Phễu khách: New → Contacted → Visited → Admitted | Lost (Lost được từ mọi trạng thái chưa kết thúc).</summary>
public enum InquiryStatus
{
    New,
    Contacted,
    Visited,
    Admitted,
    Lost
}

public enum InquirySource
{
    Web,
    Phone,
    WalkIn,
    Chatbot
}

public enum TourSlot
{
    Morning,
    Afternoon
}

public enum TourStatus
{
    Requested,
    Confirmed,
    Done,
    Cancelled,
    NoShow
}

/// <summary>Yêu cầu tư vấn của khách (người thân của người cao tuổi), chưa phải cư dân.</summary>
public class Inquiry
{
    public int Id { get; set; }

    public string FullName { get; set; } = "";

    public string Phone { get; set; } = "";

    public string? Email { get; set; }

    public string? ElderName { get; set; }

    public int? ElderAge { get; set; }

    public string? CareNeeds { get; set; }

    public int? InterestedPackageId { get; set; }

    public ServicePackage? InterestedPackage { get; set; }

    public InquirySource Source { get; set; } = InquirySource.Web;

    public InquiryStatus Status { get; set; } = InquiryStatus.New;

    /// <summary>Lễ tân/Admin phụ trách.</summary>
    public int? AssignedToId { get; set; }

    public Account? AssignedTo { get; set; }

    public string? Notes { get; set; }

    /// <summary>Cư dân được tạo khi yêu cầu chuyển Admitted.</summary>
    public int? ResidentId { get; set; }

    public Resident? Resident { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public List<TourBooking> Tours { get; set; } = [];
}

/// <summary>Lịch tham quan theo ngày (giờ cơ sở) + buổi. Sức chứa mỗi buổi: Facility:TourSlotCapacity.</summary>
public class TourBooking
{
    public int Id { get; set; }

    public int InquiryId { get; set; }

    public Inquiry Inquiry { get; set; } = null!;

    public DateOnly VisitDate { get; set; }

    public TourSlot TimeSlot { get; set; }

    public int GuestCount { get; set; } = 1;

    public string? Note { get; set; }

    public TourStatus Status { get; set; } = TourStatus.Requested;

    public int? ConfirmedById { get; set; }

    public Account? ConfirmedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Booking còn giữ chỗ (tính vào sức chứa).</summary>
    public static readonly TourStatus[] HoldingStatuses = [TourStatus.Requested, TourStatus.Confirmed];
}
