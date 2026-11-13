using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;

namespace NursingHome.DataAccessObjects;

public sealed class AccountDAO : GenericDAO<Account>
{
    public static AccountDAO Instance { get; } = new();

    private AccountDAO()
    {
    }

    public async Task<Account?> GetByEmailAsync(string email)
    {
        await using var context = CreateContext();
        return await context.Accounts.AsNoTracking().FirstOrDefaultAsync(account => account.Email == email);
    }

    public async Task<bool> EmailExistsAsync(string email, int? excludeId)
    {
        await using var context = CreateContext();
        return await context.Accounts.AnyAsync(account => account.Email == email && account.Id != excludeId);
    }

    public async Task<List<Account>> ListAsync()
    {
        await using var context = CreateContext();
        return await context.Accounts.AsNoTracking().OrderBy(account => account.Email).ToListAsync();
    }

    public async Task<List<Account>> ListActiveByRolesAsync(IReadOnlyCollection<string> roles)
    {
        await using var context = CreateContext();
        return await context.Accounts.AsNoTracking()
            .Where(account => account.IsActive && roles.Contains(account.Role))
            .OrderBy(account => account.FullName)
            .ToListAsync();
    }

    public async Task<bool> IsActiveInRolesAsync(int id, IReadOnlyCollection<string> roles)
    {
        await using var context = CreateContext();
        return await context.Accounts.AnyAsync(account => account.Id == id && account.IsActive && roles.Contains(account.Role));
    }
}

public sealed class RefreshTokenDAO : GenericDAO<RefreshToken>
{
    public static RefreshTokenDAO Instance { get; } = new();

    private RefreshTokenDAO()
    {
    }

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash)
    {
        await using var context = CreateContext();
        return await context.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(token => token.TokenHash == tokenHash);
    }

    /// <summary>Compare-and-set: chỉ thu hồi được token chưa bị thu hồi. False = request khác đã dùng nó.</summary>
    public async Task<bool> RevokeAsync(string tokenHash, DateTime now, string? replacedByTokenHash)
    {
        await using var context = CreateContext();
        int rows = await context.RefreshTokens
            .Where(token => token.TokenHash == tokenHash && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAt, now)
                .SetProperty(token => token.ReplacedByTokenHash, replacedByTokenHash));
        return rows == 1;
    }

    public async Task RevokeAllForAccountAsync(int accountId, DateTime now)
    {
        await using var context = CreateContext();
        await context.RefreshTokens
            .Where(token => token.AccountId == accountId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now));
    }
}

public sealed class NotificationDAO : GenericDAO<Notification>
{
    public static NotificationDAO Instance { get; } = new();

    private NotificationDAO()
    {
    }

    public async Task AddRangeAsync(IEnumerable<Notification> notifications)
    {
        await using var context = CreateContext();
        context.Notifications.AddRange(notifications);
        await context.SaveChangesAsync();
    }

    public async Task<List<Notification>> ListAsync(int accountId, int take)
    {
        await using var context = CreateContext();
        return await context.Notifications.AsNoTracking()
            .Where(notification => notification.AccountId == accountId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> UnreadCountAsync(int accountId)
    {
        await using var context = CreateContext();
        return await context.Notifications.CountAsync(notification => notification.AccountId == accountId && !notification.IsRead);
    }

    public async Task<bool> MarkReadAsync(long id, int accountId)
    {
        await using var context = CreateContext();
        int rows = await context.Notifications
            .Where(notification => notification.Id == id && notification.AccountId == accountId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.IsRead, true));
        return rows == 1;
    }

    public async Task MarkAllReadAsync(int accountId)
    {
        await using var context = CreateContext();
        await context.Notifications
            .Where(notification => notification.AccountId == accountId && !notification.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.IsRead, true));
    }
}
