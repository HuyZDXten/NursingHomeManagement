using NursingHome.DTOs.Admin;
using NursingHome.DTOs.Common;

namespace NursingHome.Services.Interfaces;

// ======================= BL-V5: hệ thống & truyền thông =======================

/// <summary>UC-13: nhật ký thao tác. Ghi nhật ký là "cố gắng hết sức": lỗi ghi chỉ ghi log, không làm hỏng thao tác chính đã thành công.</summary>
public interface IAuditService
{
    /// <param name="actor">null khi thao tác không có người dùng (job nền, webhook).</param>
    Task RecordAsync(CurrentUser? actor, string action, string entityType, string? entityId, string summary, object? data = null, string? ip = null);

    Task<PageDto<AuditLogDto>> ListAsync(AuditQuery query);

    Task<List<string>> ListActionsAsync();
}

/// <summary>UC-129: cấu hình cơ sở ghi đè appsettings, áp dụng ngay không cần khởi động lại.</summary>
public interface ISettingsService
{
    Task<List<SettingDto>> ListAsync();

    Task<List<SettingDto>> SaveAsync(CurrentUser user, SaveSettingsRequest request);

    /// <summary>Bỏ ghi đè, quay về giá trị trong appsettings.</summary>
    Task<List<SettingDto>> ResetAsync(CurrentUser user, string key);

    /// <summary>Nạp lại các ghi đè từ DB vào bộ nhớ (gọi lúc khởi động API).</summary>
    Task ReloadAsync();
}

/// <summary>UC-130: danh mục dùng chung (quan hệ gia đình, bộ phận, chức danh).</summary>
public interface ILookupService
{
    Task<List<LookupDto>> ListAsync(string? category, bool includeInactive);

    Task<LookupDto> CreateAsync(SaveLookupRequest request);

    Task<LookupDto> UpdateAsync(int id, SaveLookupRequest request);

    Task DeleteAsync(int id);
}

/// <summary>UC-119 thông báo hàng loạt của Admin.</summary>
public interface IAnnouncementService
{
    Task<AnnouncementDto> SendAsync(CurrentUser user, CreateAnnouncementRequest request);

    Task<List<AnnouncementDto>> ListAsync();
}

/// <summary>UC-120: mỗi người chọn kênh email muốn nhận. Chuông thông báo trong ứng dụng luôn gửi.</summary>
public interface INotificationPreferenceService
{
    Task<NotificationPreferenceDto> GetAsync(CurrentUser user);

    Task<NotificationPreferenceDto> SaveAsync(CurrentUser user, NotificationPreferenceDto request);
}

/// <summary>UC-19 tin tức và UC-115 album (quản trị + trang công khai).</summary>
public interface IPostService
{
    Task<PageDto<PostSummaryDto>> ListAsync(string? kind, int page, int pageSize);

    Task<PostDto> GetAsync(int id);

    Task<PostDto> CreateAsync(CurrentUser user, SavePostRequest request);

    Task<PostDto> UpdateAsync(int id, SavePostRequest request);

    Task<PostDto> SetPublishedAsync(int id, bool publish);

    Task DeleteAsync(int id);

    Task<PageDto<PostSummaryDto>> ListPublishedAsync(string? kind, int page, int pageSize);

    Task<PublicPostDto> GetPublishedAsync(string slug);
}
