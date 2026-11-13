using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class AccountRepository : IAccountRepository
{
    private static AccountDAO Dao => AccountDAO.Instance;

    public Task<Account?> GetByIdAsync(int id) => Dao.GetByIdAsync(id);

    public Task<Account?> GetByEmailAsync(string email) => Dao.GetByEmailAsync(email);

    public Task<bool> EmailExistsAsync(string email, int? excludeId) => Dao.EmailExistsAsync(email, excludeId);

    public Task<List<Account>> ListAsync() => Dao.ListAsync();

    public Task<List<Account>> ListActiveByRolesAsync(IReadOnlyCollection<string> roles) => Dao.ListActiveByRolesAsync(roles);

    public Task<bool> IsActiveInRolesAsync(int id, IReadOnlyCollection<string> roles) => Dao.IsActiveInRolesAsync(id, roles);

    public Task<Account> AddAsync(Account account) => Dao.AddAsync(account);

    public Task UpdateAsync(Account account) => Dao.UpdateAsync(account);
}

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private static RefreshTokenDAO Dao => RefreshTokenDAO.Instance;

    public Task AddAsync(RefreshToken token) => Dao.AddAsync(token);

    public Task<RefreshToken?> GetByHashAsync(string tokenHash) => Dao.GetByHashAsync(tokenHash);

    public Task<bool> RevokeAsync(string tokenHash, DateTime now, string? replacedByTokenHash) =>
        Dao.RevokeAsync(tokenHash, now, replacedByTokenHash);

    public Task RevokeAllForAccountAsync(int accountId, DateTime now) => Dao.RevokeAllForAccountAsync(accountId, now);
}

public class NotificationRepository : INotificationRepository
{
    private static NotificationDAO Dao => NotificationDAO.Instance;

    public Task AddRangeAsync(IEnumerable<Notification> notifications) => Dao.AddRangeAsync(notifications);

    public Task<List<Notification>> ListAsync(int accountId, int take) => Dao.ListAsync(accountId, take);

    public Task<int> UnreadCountAsync(int accountId) => Dao.UnreadCountAsync(accountId);

    public Task<bool> MarkReadAsync(long id, int accountId) => Dao.MarkReadAsync(id, accountId);

    public Task MarkAllReadAsync(int accountId) => Dao.MarkAllReadAsync(accountId);
}
