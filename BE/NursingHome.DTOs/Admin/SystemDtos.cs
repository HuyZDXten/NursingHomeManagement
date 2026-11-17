using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Admin;

// ===== BL-V5: nhật ký thao tác (UC-13), cấu hình & danh mục (UC-129/130), thông báo (UC-119/120), tin tức & album (UC-19/115) =====

public class PageDto<T>
{
    public List<T> Items { get; set; } = [];

    public int Total { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}

// ---------------- UC-13 nhật ký thao tác ----------------

public class AuditLogDto
{
    public long Id { get; set; }

    public int? AccountId { get; set; }

    public string ActorName { get; set; } = "";

    public string Action { get; set; } = "";

    public string EntityType { get; set; } = "";

    public string? EntityId { get; set; }

    public string Summary { get; set; } = "";

    public string? DataJson { get; set; }

    public string? Ip { get; set; }

    public DateTime At { get; set; }
}

public class AuditQuery
{
    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public int? AccountId { get; set; }

    public string? Action { get; set; }

    public string? EntityType { get; set; }

    /// <summary>Tìm trong mô tả và tên người thực hiện.</summary>
    public string? Search { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

// ---------------- UC-129 cấu hình cơ sở ----------------

/// <summary>Kind: Text | Integer | Decimal. IsOverridden = đang dùng giá trị Admin đặt (không phải appsettings).</summary>
public class SettingDto
{
    public string Key { get; set; } = "";

    public string Label { get; set; } = "";

    public string Group { get; set; } = "";

    public string Kind { get; set; } = "Text";

    public string Value { get; set; } = "";

    public string DefaultValue { get; set; } = "";

    public bool IsOverridden { get; set; }

    public decimal? Min { get; set; }

    public decimal? Max { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class SettingValueRequest
{
    [Required, StringLength(100)]
    public string Key { get; set; } = "";

    [StringLength(500)]
    public string Value { get; set; } = "";
}

public class SaveSettingsRequest
{
    [Required, MinLength(1, ErrorMessage = "Chưa có giá trị nào để lưu.")]
    public List<SettingValueRequest> Values { get; set; } = [];
}

// ---------------- UC-130 danh mục ----------------

public class LookupDto
{
    public int Id { get; set; }

    public string Category { get; set; } = "";

    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }
}

public class SaveLookupRequest
{
    /// <summary>Chỉ dùng khi tạo: Relationship | Department | Position. Không đổi được sau khi tạo.</summary>
    [StringLength(30)]
    public string? Category { get; set; }

    [Required(ErrorMessage = "Nhập mã."), StringLength(50)]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Nhập tên hiển thị."), StringLength(100)]
    public string Name { get; set; } = "";

    [Range(0, 9999)]
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

// ---------------- UC-119 thông báo hàng loạt, UC-120 kênh nhận ----------------

public class AnnouncementDto
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public string Body { get; set; } = "";

    public string Audience { get; set; } = "";

    public string? Role { get; set; }

    public bool SendEmail { get; set; }

    public int RecipientCount { get; set; }

    public int EmailSentCount { get; set; }

    public string CreatedByName { get; set; } = "";

    public DateTime CreatedAt { get; set; }
}

public class CreateAnnouncementRequest
{
    [Required(ErrorMessage = "Nhập tiêu đề."), StringLength(200)]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Nhập nội dung."), StringLength(2000)]
    public string Body { get; set; } = "";

    /// <summary>All | Family | Staff | Role.</summary>
    [Required]
    public string Audience { get; set; } = "";

    /// <summary>Bắt buộc khi Audience = Role.</summary>
    public string? Role { get; set; }

    /// <summary>Gửi thêm email cho người đã bật kênh "thông báo chung". Chỉ có tác dụng khi đã cấu hình SMTP.</summary>
    public bool SendEmail { get; set; }
}

public class NotificationPreferenceDto
{
    public bool EmailInvoices { get; set; } = true;

    public bool EmailReminders { get; set; } = true;

    public bool EmailAnnouncements { get; set; } = true;
}

// ---------------- UC-19 tin tức, UC-115 album ----------------

public class PostImageDto
{
    public int Id { get; set; }

    public string Url { get; set; } = "";

    public string? Caption { get; set; }

    public int SortOrder { get; set; }
}

public class PostSummaryDto
{
    public int Id { get; set; }

    /// <summary>News | Album.</summary>
    public string Kind { get; set; } = "";

    public string Title { get; set; } = "";

    public string Slug { get; set; } = "";

    public string? Summary { get; set; }

    public string? CoverUrl { get; set; }

    public bool IsPublished { get; set; }

    public DateTime? PublishedAt { get; set; }

    public int ImageCount { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class PostDto : PostSummaryDto
{
    public string Content { get; set; } = "";

    public bool ConsentConfirmed { get; set; }

    public string CreatedByName { get; set; } = "";

    public DateTime? UpdatedAt { get; set; }

    public List<PostImageDto> Images { get; set; } = [];
}

/// <summary>Bài công khai: không lộ người tạo, xác nhận đồng ý hay trạng thái nháp.</summary>
public class PublicPostDto
{
    public int Id { get; set; }

    public string Kind { get; set; } = "";

    public string Title { get; set; } = "";

    public string Slug { get; set; } = "";

    public string? Summary { get; set; }

    public string? CoverUrl { get; set; }

    public DateTime? PublishedAt { get; set; }

    public string Content { get; set; } = "";

    public List<PostImageDto> Images { get; set; } = [];
}

public class SavePostImageRequest
{
    [Required, StringLength(500)]
    public string Url { get; set; } = "";

    [StringLength(200)]
    public string? Caption { get; set; }
}

public class SavePostRequest
{
    /// <summary>News | Album. Chỉ dùng khi tạo; không đổi loại sau khi tạo.</summary>
    [Required]
    public string Kind { get; set; } = "News";

    [Required(ErrorMessage = "Nhập tiêu đề."), StringLength(200)]
    public string Title { get; set; } = "";

    [StringLength(400)]
    public string? Summary { get; set; }

    [Required(ErrorMessage = "Nhập nội dung."), StringLength(20000)]
    public string Content { get; set; } = "";

    [StringLength(500)]
    public string? CoverUrl { get; set; }

    /// <summary>Album: xác nhận đã có sự đồng ý của cư dân/gia đình cho ảnh xuất hiện công khai.</summary>
    public bool ConsentConfirmed { get; set; }

    [MaxLength(30, ErrorMessage = "Tối đa 30 ảnh.")]
    public List<SavePostImageRequest> Images { get; set; } = [];
}
