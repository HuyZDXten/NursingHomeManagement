using NursingHome.BusinessObjects.Entities;

namespace NursingHome.Repositories.Interfaces;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(int id);

    Task<Account?> GetByEmailAsync(string email);

    Task<bool> EmailExistsAsync(string email, int? excludeId);

    Task<List<Account>> ListAsync();

    Task<List<Account>> ListActiveByRolesAsync(IReadOnlyCollection<string> roles);

    Task<bool> IsActiveInRolesAsync(int id, IReadOnlyCollection<string> roles);

    Task<Account> AddAsync(Account account);

    Task UpdateAsync(Account account);
}

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token);

    Task<RefreshToken?> GetByHashAsync(string tokenHash);

    Task<bool> RevokeAsync(string tokenHash, DateTime now, string? replacedByTokenHash);

    Task RevokeAllForAccountAsync(int accountId, DateTime now);
}

public interface INotificationRepository
{
    Task AddRangeAsync(IEnumerable<Notification> notifications);

    Task<List<Notification>> ListAsync(int accountId, int take);

    Task<int> UnreadCountAsync(int accountId);

    Task<bool> MarkReadAsync(long id, int accountId);

    Task MarkAllReadAsync(int accountId);
}
