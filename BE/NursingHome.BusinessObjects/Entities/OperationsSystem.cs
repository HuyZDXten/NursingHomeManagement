namespace NursingHome.BusinessObjects.Entities;

// MODULE VẬN HÀNH (Viên) — BL-V5 hệ thống & truyền thông (schema mục 9.11).

/// <summary>UC-13: nhật ký thao tác. Chỉ thêm; ActorName giữ tên lúc ghi (đổi tên tài khoản sau đó không sửa lịch sử).</summary>
public class AuditLog
{
    public long Id { get; set; }

    public int? AccountId { get; set; }

    public string? ActorName { get; set; }

    public string Action { get; set; } = "";

    public string EntityType { get; set; } = "";

    public string? EntityId { get; set; }

    public string Summary { get; set; } = "";

    public string? DataJson { get; set; }

    public string? Ip { get; set; }

    public DateTime At { get; set; }
}

/// <summary>UC-129: giá trị cấu hình cơ sở ghi đè appsettings. Khóa không có dòng = dùng appsettings.</summary>
public class SystemSetting
{
    public string Key { get; set; } = "";

    public string Value { get; set; } = "";

    public DateTime UpdatedAt { get; set; }

    public int? UpdatedById { get; set; }
}

/// <summary>UC-130: danh mục dùng chung. Category: Relationship | Department | Position (<see cref="LookupCategories"/>).</summary>
public class Lookup
{
    public int Id { get; set; }

    public string Category { get; set; } = "";

    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

public static class LookupCategories
{
    public const string Relationship = "Relationship";
    public const string Department = "Department";
    public const string Position = "Position";

    public static readonly string[] All = [Relationship, Department, Position];

    public static bool IsValid(string category) => All.Contains(category);
}

/// <summary>UC-119: thông báo hàng loạt. Audience: All | Family | Staff | Role (kèm <see cref="Role"/>).</summary>
public class Announcement
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public string Body { get; set; } = "";

    public string Audience { get; set; } = "";

    public string? Role { get; set; }

    public bool SendEmail { get; set; }

    public int RecipientCount { get; set; }

    public int EmailSentCount { get; set; }

    public int CreatedById { get; set; }

    public Account CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}

public static class AnnouncementAudiences
{
    public const string All = "All";
    public const string Family = "Family";
    public const string Staff = "Staff";
    public const string Role = "Role";

    public static readonly string[] Values = [All, Family, Staff, Role];
}

/// <summary>UC-120: lựa chọn kênh email của từng người. Chưa có dòng = bật hết. Chuông thông báo trong ứng dụng luôn gửi.</summary>
public class NotificationPreference
{
    public int AccountId { get; set; }

    public bool EmailInvoices { get; set; } = true;

    public bool EmailReminders { get; set; } = true;

    public bool EmailAnnouncements { get; set; } = true;

    public DateTime UpdatedAt { get; set; }
}

public enum PostKind
{
    News,
    Album
}

/// <summary>UC-19 tin tức, UC-115 album. Nội dung là chữ thuần. Album chỉ đăng khi đã xác nhận có sự đồng ý.</summary>
public class Post
{
    public int Id { get; set; }

    public PostKind Kind { get; set; }

    public string Title { get; set; } = "";

    public string Slug { get; set; } = "";

    public string? Summary { get; set; }

    public string Content { get; set; } = "";

    public string? CoverUrl { get; set; }

    public bool ConsentConfirmed { get; set; }

    public bool IsPublished { get; set; }

    public DateTime? PublishedAt { get; set; }

    public int CreatedById { get; set; }

    public Account CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public List<PostImage> Images { get; set; } = [];
}

public class PostImage
{
    public int Id { get; set; }

    public int PostId { get; set; }

    public string Url { get; set; } = "";

    public string? Caption { get; set; }

    public int SortOrder { get; set; }
}
