using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.DataAccessObjects;

/// <summary>Kênh email người dùng có thể tắt (UC-120). Thông báo trong ứng dụng (chuông) không tắt được.</summary>
public enum EmailChannel
{
    Invoices,
    Reminders,
    Announcements
}

/// <summary>Một bài kèm số ảnh, dùng cho danh sách (không tải Content và danh sách ảnh).</summary>
public sealed record PostRow(Post Post, int ImageCount);

/// <summary>MODULE VẬN HÀNH (Viên) — BL-V5: thông báo hàng loạt (UC-119), kênh nhận (UC-120), tin tức & album (UC-19, UC-115).</summary>
public sealed class CommsDAO : GenericDAO<Post>
{
    public static CommsDAO Instance { get; } = new();

    private CommsDAO()
    {
    }

    // ---------------- Thông báo hàng loạt ----------------

    public Task<Announcement> AddAnnouncementAsync(Announcement announcement) => AddEntityAsync(announcement);

    public async Task SetAnnouncementResultAsync(int id, int recipientCount, int emailSentCount)
    {
        await using var context = CreateContext();
        await context.Announcements.Where(item => item.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(item => item.RecipientCount, recipientCount).SetProperty(item => item.EmailSentCount, emailSentCount));
    }

    public async Task<List<Announcement>> ListAnnouncementsAsync(int take)
    {
        await using var context = CreateContext();
        return await context.Announcements.AsNoTracking().Include(item => item.CreatedBy)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Take(take).ToListAsync();
    }

    // ---------------- Kênh nhận ----------------

    public async Task<NotificationPreference?> GetPreferenceAsync(int accountId)
    {
        await using var context = CreateContext();
        return await context.NotificationPreferences.AsNoTracking().FirstOrDefaultAsync(item => item.AccountId == accountId);
    }

    /// <summary>Hai request lưu cùng lúc: bên thua đọc lại rồi cập nhật.</summary>
    public async Task UpsertPreferenceAsync(NotificationPreference preference)
    {
        try
        {
            await using var context = CreateContext();
            var existing = await context.NotificationPreferences.FirstOrDefaultAsync(item => item.AccountId == preference.AccountId);
            if (existing is null)
            {
                context.NotificationPreferences.Add(preference);
            }
            else
            {
                Apply(preference, existing);
            }

            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await using var retry = CreateContext();
            var existing = await retry.NotificationPreferences.FirstAsync(item => item.AccountId == preference.AccountId);
            Apply(preference, existing);
            await retry.SaveChangesAsync();
        }
    }

    private static void Apply(NotificationPreference from, NotificationPreference to)
    {
        to.EmailInvoices = from.EmailInvoices;
        to.EmailReminders = from.EmailReminders;
        to.EmailAnnouncements = from.EmailAnnouncements;
        to.UpdatedAt = from.UpdatedAt;
    }

    /// <summary>Trong các email đã cho, những tài khoản đã TẮT kênh này (viết thường). Email không thuộc tài khoản nào = không tắt.</summary>
    public async Task<HashSet<string>> ListOptedOutEmailsAsync(IReadOnlyCollection<string> emails, EmailChannel channel)
    {
        if (emails.Count == 0)
        {
            return [];
        }

        await using var context = CreateContext();
        var query = from account in context.Accounts.AsNoTracking()
                    join preference in context.NotificationPreferences.AsNoTracking() on account.Id equals preference.AccountId
                    where emails.Contains(account.Email)
                    select new { account.Email, preference.EmailInvoices, preference.EmailReminders, preference.EmailAnnouncements };
        var rows = await query.ToListAsync();
        return rows
            .Where(row => channel switch
            {
                EmailChannel.Invoices => !row.EmailInvoices,
                EmailChannel.Reminders => !row.EmailReminders,
                _ => !row.EmailAnnouncements
            })
            .Select(row => row.Email.ToLowerInvariant())
            .ToHashSet();
    }

    // ---------------- Tin tức & album ----------------

    public async Task<(List<PostRow> Items, int Total)> ListPostsAsync(bool publishedOnly, PostKind? kind, int skip, int take)
    {
        await using var context = CreateContext();
        var query = context.Posts.AsNoTracking().AsQueryable();
        if (publishedOnly)
        {
            query = query.Where(item => item.IsPublished);
        }

        if (kind is not null)
        {
            query = query.Where(item => item.Kind == kind);
        }

        int total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(item => item.PublishedAt ?? item.CreatedAt).ThenByDescending(item => item.Id)
            .Skip(skip).Take(take)
            .Select(item => new
            {
                item.Id, item.Kind, item.Title, item.Slug, item.Summary, item.CoverUrl, item.ConsentConfirmed,
                item.IsPublished, item.PublishedAt, item.CreatedById, item.CreatedAt, item.UpdatedAt,
                ImageCount = item.Images.Count
            })
            .ToListAsync();
        return (rows.Select(row => new PostRow(new Post
        {
            Id = row.Id, Kind = row.Kind, Title = row.Title, Slug = row.Slug, Summary = row.Summary, CoverUrl = row.CoverUrl,
            ConsentConfirmed = row.ConsentConfirmed, IsPublished = row.IsPublished, PublishedAt = row.PublishedAt,
            CreatedById = row.CreatedById, CreatedAt = row.CreatedAt, UpdatedAt = row.UpdatedAt
        }, row.ImageCount)).ToList(), total);
    }

    public async Task<Post?> GetPostAsync(int id)
    {
        await using var context = CreateContext();
        return await context.Posts.AsNoTracking().Include(item => item.CreatedBy).Include(item => item.Images)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task<Post?> GetPublishedBySlugAsync(string slug)
    {
        await using var context = CreateContext();
        return await context.Posts.AsNoTracking().Include(item => item.CreatedBy).Include(item => item.Images)
            .FirstOrDefaultAsync(item => item.Slug == slug && item.IsPublished);
    }

    public async Task<bool> SlugExistsAsync(string slug)
    {
        await using var context = CreateContext();
        return await context.Posts.AnyAsync(item => item.Slug == slug);
    }

    public async Task<Post> CreatePostAsync(Post post)
    {
        try
        {
            return await AddEntityAsync(post);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("Đường dẫn bài viết bị trùng, hãy đổi tiêu đề rồi lưu lại.");
        }
    }

    /// <summary>
    /// Sửa bài và (nếu truyền) thay toàn bộ ảnh trong MỘT transaction. <paramref name="apply"/> chạy trên bản đang theo dõi,
    /// có thể ném BusinessRuleException để hủy cả thao tác.
    /// </summary>
    public async Task<Post?> UpdatePostAsync(int id, Action<Post> apply, List<PostImage>? images)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var post = await context.Posts.Include(item => item.Images).FirstOrDefaultAsync(item => item.Id == id);
        if (post is null)
        {
            return null;
        }

        if (images is not null)
        {
            context.PostImages.RemoveRange(post.Images);
            post.Images = images;
        }

        apply(post);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return post;
    }

    public async Task<bool> DeletePostAsync(int id)
    {
        await using var context = CreateContext();
        return await context.Posts.Where(item => item.Id == id).ExecuteDeleteAsync() > 0;
    }
}
