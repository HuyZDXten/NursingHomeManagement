using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;

namespace NursingHome.Repositories.Interfaces;

/// <summary>BL-V5, UC-13: nhật ký thao tác (chỉ thêm và đọc).</summary>
public interface IAuditRepository
{
    Task AddAsync(AuditLog log);

    Task<(List<AuditLog> Items, int Total)> SearchAsync(
        DateTime? fromUtc, DateTime? toUtc, int? accountId, string? action, string? entityType, string? search, int skip, int take);

    Task<List<string>> ListActionsAsync();
}

/// <summary>BL-V5, UC-129/130: cấu hình cơ sở và danh mục.</summary>
public interface ISystemRepository
{
    Task<List<SystemSetting>> ListSettingsAsync();

    Task SaveSettingsAsync(IReadOnlyDictionary<string, string> values, int? updatedById, DateTime now);

    Task<bool> DeleteSettingAsync(string key);

    Task<List<Lookup>> ListLookupsAsync(string? category, bool includeInactive);

    Task<Lookup?> GetLookupAsync(int id);

    Task<Lookup> AddLookupAsync(Lookup lookup);

    Task<Lookup?> UpdateLookupAsync(int id, Action<Lookup> apply);

    Task<bool> DeleteLookupAsync(int id);
}

/// <summary>BL-V5, UC-119/120/19/115: thông báo hàng loạt, kênh nhận email, tin tức & album.</summary>
public interface ICommsRepository
{
    Task<Announcement> AddAnnouncementAsync(Announcement announcement);

    Task SetAnnouncementResultAsync(int id, int recipientCount, int emailSentCount);

    Task<List<Announcement>> ListAnnouncementsAsync(int take);

    Task<NotificationPreference?> GetPreferenceAsync(int accountId);

    Task UpsertPreferenceAsync(NotificationPreference preference);

    Task<HashSet<string>> ListOptedOutEmailsAsync(IReadOnlyCollection<string> emails, EmailChannel channel);

    Task<(List<PostRow> Items, int Total)> ListPostsAsync(bool publishedOnly, PostKind? kind, int skip, int take);

    Task<Post?> GetPostAsync(int id);

    Task<Post?> GetPublishedBySlugAsync(string slug);

    Task<bool> SlugExistsAsync(string slug);

    Task<Post> CreatePostAsync(Post post);

    Task<Post?> UpdatePostAsync(int id, Action<Post> apply, List<PostImage>? images);

    Task<bool> DeletePostAsync(int id);
}
