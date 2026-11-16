using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Rooms;

// ===== BL-V3: loại phòng & phụ thu (UC-36), giữ giường (UC-41), báo hỏng & sửa chữa (UC-42) =====

public class RoomTypeDto
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public decimal DailySurcharge { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }
}

public class SaveRoomTypeRequest
{
    [Required(ErrorMessage = "Nhập tên loại phòng."), StringLength(100)]
    public string Name { get; set; } = "";

    [Range(typeof(decimal), "0", "99999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Phụ thu/ngày từ 0 đến 99.999.999 đ.")]
    public decimal DailySurcharge { get; set; }

    [StringLength(300)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class BedReservationDto
{
    public int Id { get; set; }

    public int BedId { get; set; }

    public string BedLabel { get; set; } = "";

    public int? InquiryId { get; set; }

    public string HolderName { get; set; } = "";

    public string? Phone { get; set; }

    public string? Note { get; set; }

    /// <summary>Active | Expired | Converted | Released (Expired = Active đã quá hạn).</summary>
    public string Status { get; set; } = "";

    public string? ReservedByName { get; set; }

    public DateTime ReservedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? ClosedAt { get; set; }
}

public class CreateBedReservationRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Chọn giường.")]
    public int BedId { get; set; }

    /// <summary>Gắn với yêu cầu tư vấn (S3-V1) thì khi nhập viện từ yêu cầu đó, xếp vào giường này được.</summary>
    public int? InquiryId { get; set; }

    [Required(ErrorMessage = "Nhập tên người giữ."), StringLength(150)]
    public string HolderName { get; set; } = "";

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    /// <summary>Ngày hết giữ (giờ cơ sở, giữ tới 23:59 ngày đó), tối đa 7 ngày.</summary>
    public DateOnly ExpiresOn { get; set; }
}

public class MaintenanceRequestDto
{
    public int Id { get; set; }

    public int RoomId { get; set; }

    public string RoomNumber { get; set; } = "";

    public int? BedId { get; set; }

    public string? BedNumber { get; set; }

    public string Title { get; set; } = "";

    public string? Description { get; set; }

    /// <summary>Low | Normal | High.</summary>
    public string Priority { get; set; } = "";

    /// <summary>Open | InProgress | Done | Cancelled.</summary>
    public string Status { get; set; } = "";

    public bool BlocksBed { get; set; }

    public string? ReportedByName { get; set; }

    public DateTime ReportedAt { get; set; }

    public string? AssignedTo { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public string? ResolutionNote { get; set; }
}

public class CreateMaintenanceRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Chọn phòng.")]
    public int RoomId { get; set; }

    public int? BedId { get; set; }

    [Required(ErrorMessage = "Nhập nội dung hỏng."), StringLength(200)]
    public string Title { get; set; } = "";

    [StringLength(1000)]
    public string? Description { get; set; }

    [RegularExpression("^(Low|Normal|High)$", ErrorMessage = "Mức độ không hợp lệ.")]
    public string Priority { get; set; } = "Normal";

    /// <summary>Không xếp người vào giường này tới khi sửa xong (cần chọn giường).</summary>
    public bool BlocksBed { get; set; }
}

public class UpdateMaintenanceRequest
{
    [Required, RegularExpression("^(InProgress|Done|Cancelled)$", ErrorMessage = "Trạng thái không hợp lệ.")]
    public string Status { get; set; } = "";

    [StringLength(150)]
    public string? AssignedTo { get; set; }

    [StringLength(500)]
    public string? ResolutionNote { get; set; }
}
