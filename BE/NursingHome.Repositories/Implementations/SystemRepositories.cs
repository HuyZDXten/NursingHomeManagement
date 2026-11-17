using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class AuditRepository : IAuditRepository
{
    private static AuditDAO Dao => AuditDAO.Instance;

    public Task AddAsync(AuditLog log) => Dao.AddAsync(log);

    public Task<(List<AuditLog> Items, int Total)> SearchAsync(
        DateTime? fromUtc, DateTime? toUtc, int? accountId, string? action, string? entityType, string? search, int skip, int take) =>
        Dao.SearchAsync(fromUtc, toUtc, accountId, action, entityType, search, skip, take);

    public Task<List<string>> ListActionsAsync() => Dao.ListActionsAsync();
}

public class SystemRepository : ISystemRepository
{
    private static SystemDAO Dao => SystemDAO.Instance;

    public Task<List<SystemSetting>> ListSettingsAsync() => Dao.ListSettingsAsync();

    public Task SaveSettingsAsync(IReadOnlyDictionary<string, string> values, int? updatedById, DateTime now) =>
        Dao.SaveSettingsAsync(values, updatedById, now);

    public Task<bool> DeleteSettingAsync(string key) => Dao.DeleteSettingAsync(key);

    public Task<List<Lookup>> ListLookupsAsync(string? category, bool includeInactive) => Dao.ListLookupsAsync(category, includeInactive);

    public Task<Lookup?> GetLookupAsync(int id) => Dao.GetLookupAsync(id);

    public Task<Lookup> AddLookupAsync(Lookup lookup) => Dao.AddLookupAsync(lookup);

    public Task<Lookup?> UpdateLookupAsync(int id, Action<Lookup> apply) => Dao.UpdateLookupAsync(id, apply);

    public Task<bool> DeleteLookupAsync(int id) => Dao.DeleteLookupAsync(id);
}

public class CommsRepository : ICommsRepository
{
    private static CommsDAO Dao => CommsDAO.Instance;

    public Task<Announcement> AddAnnouncementAsync(Announcement announcement) => Dao.AddAnnouncementAsync(announcement);

    public Task SetAnnouncementResultAsync(int id, int recipientCount, int emailSentCount) =>
        Dao.SetAnnouncementResultAsync(id, recipientCount, emailSentCount);

    public Task<List<Announcement>> ListAnnouncementsAsync(int take) => Dao.ListAnnouncementsAsync(take);

    public Task<NotificationPreference?> GetPreferenceAsync(int accountId) => Dao.GetPreferenceAsync(accountId);

    public Task UpsertPreferenceAsync(NotificationPreference preference) => Dao.UpsertPreferenceAsync(preference);

    public Task<HashSet<string>> ListOptedOutEmailsAsync(IReadOnlyCollection<string> emails, EmailChannel channel) =>
        Dao.ListOptedOutEmailsAsync(emails, channel);

    public Task<(List<PostRow> Items, int Total)> ListPostsAsync(bool publishedOnly, PostKind? kind, int skip, int take) =>
        Dao.ListPostsAsync(publishedOnly, kind, skip, take);

    public Task<Post?> GetPostAsync(int id) => Dao.GetPostAsync(id);

    public Task<Post?> GetPublishedBySlugAsync(string slug) => Dao.GetPublishedBySlugAsync(slug);

    public Task<bool> SlugExistsAsync(string slug) => Dao.SlugExistsAsync(slug);

    public Task<Post> CreatePostAsync(Post post) => Dao.CreatePostAsync(post);

    public Task<Post?> UpdatePostAsync(int id, Action<Post> apply, List<PostImage>? images) => Dao.UpdatePostAsync(id, apply, images);

    public Task<bool> DeletePostAsync(int id) => Dao.DeletePostAsync(id);
}
