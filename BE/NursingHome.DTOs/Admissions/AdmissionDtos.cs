using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Admissions;

// MODULE VẬN HÀNH (Viên) — S3-V1 tiếp nhận & tham quan (UC-17, 18, 21, 22).

/// <summary>Form "Nhận tư vấn" ở trang chủ (ẩn danh). Chatbot (S4-V1) dùng lại.</summary>
public class CreateInquiryRequest
{
    [Required, MaxLength(150)]
    public string FullName { get; set; } = "";

    [Required, Phone, MaxLength(30)]
    public string Phone { get; set; } = "";

    [EmailAddress, MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(150)]
    public string? ElderName { get; set; }

    [Range(40, 120)]
    public int? ElderAge { get; set; }

    [MaxLength(1000)]
    public string? CareNeeds { get; set; }

    public int? InterestedPackageId { get; set; }
}

/// <summary>Đặt lịch tham quan (ẩn danh). TimeSlot: Morning | Afternoon.</summary>
public class BookTourRequest
{
    [Required, MaxLength(150)]
    public string FullName { get; set; } = "";

    [Required, Phone, MaxLength(30)]
    public string Phone { get; set; } = "";

    [EmailAddress, MaxLength(256)]
    public string? Email { get; set; }

    [Required]
    public DateOnly VisitDate { get; set; }

    [Required, MaxLength(20)]
    public string TimeSlot { get; set; } = "";

    [Range(1, 10)]
    public int GuestCount { get; set; } = 1;

    [MaxLength(500)]
    public string? Note { get; set; }
}

/// <summary>Entity set OData "Inquiries" (FrontDesk).</summary>
public class InquiryDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public string? ElderName { get; set; }
    public int? ElderAge { get; set; }
    public string? CareNeeds { get; set; }
    public int? InterestedPackageId { get; set; }
    public string? InterestedPackageName { get; set; }
    public string Source { get; set; } = "";
    public string Status { get; set; } = "";
    public int? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public string? Notes { get; set; }
    public int? ResidentId { get; set; }
    public string? ResidentCode { get; set; }
    public int TourCount { get; set; }

    /// <summary>Lịch tham quan gần nhất (theo ngày).</summary>
    public DateOnly? LastTourDate { get; set; }
    public string? LastTourSlot { get; set; }
    public string? LastTourStatus { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class TourBookingDto
{
    public int Id { get; set; }
    public int InquiryId { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public DateOnly VisitDate { get; set; }
    public string TimeSlot { get; set; } = "";
    public int GuestCount { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "";
    public int? ConfirmedById { get; set; }
    public string? ConfirmedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Số chỗ còn lại của một buổi — endpoint công khai chỉ trả thông tin này.</summary>
public class TourSlotAvailabilityDto
{
    public DateOnly Date { get; set; }
    public string Slot { get; set; } = "";
    public int Capacity { get; set; }
    public int Remaining { get; set; }
}

/// <summary>Lễ tân đổi trạng thái / giao người phụ trách / ghi chú. Status: New | Contacted | Visited | Lost.</summary>
public class UpdateInquiryRequest
{
    [Required, MaxLength(20)]
    public string Status { get; set; } = "";

    public int? AssignedToId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>"Nhập viện": gắn cư dân vừa tạo (POST /api/residents) vào yêu cầu.</summary>
public class AdmitInquiryRequest
{
    [Range(1, int.MaxValue)]
    public int ResidentId { get; set; }
}
