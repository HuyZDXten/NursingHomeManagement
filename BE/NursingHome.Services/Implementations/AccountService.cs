using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Accounts;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>UC-09..UC-12: Admin quản lý tài khoản. Không có tự đăng ký Admin.</summary>
public class AccountService(IAccountRepository accounts, IRefreshTokenRepository refreshTokens, FacilityClock clock) : IAccountService
{
    public async Task<List<AccountDto>> ListAsync() => (await accounts.ListAsync()).Select(account => account.ToDto()).ToList();

    public async Task<AccountDto> GetAsync(int id) => (await FindAsync(id)).ToDto();

    public async Task<AccountDto> CreateAsync(CreateAccountRequest request)
    {
        ValidateRole(request.Role);
        string email = request.Email.Trim();
        if (await accounts.EmailExistsAsync(email, null))
        {
            throw new ConflictException("Email đã được sử dụng.");
        }

        var account = await accounts.AddAsync(new Account
        {
            Email = email,
            FullName = request.FullName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Role = request.Role,
            PasswordHash = PasswordHasher.Hash(request.Password),
            IsActive = true,
            CreatedAt = clock.UtcNow
        });
        return account.ToDto();
    }

    public async Task<AccountDto> UpdateAsync(CurrentUser user, int id, UpdateAccountRequest request)
    {
        ValidateRole(request.Role);
        var account = await FindAsync(id);
        if (id == user.AccountId && (!request.IsActive || request.Role != Roles.Admin))
        {
            throw new BusinessRuleException("Không thể tự khóa hoặc tự bỏ quyền Admin của chính mình.");
        }

        bool sessionChanged = account.Role != request.Role || (account.IsActive && !request.IsActive);
        account.FullName = request.FullName.Trim();
        account.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        account.Role = request.Role;
        account.IsActive = request.IsActive;
        account.UpdatedAt = clock.UtcNow;
        await accounts.UpdateAsync(account);

        // Đổi role/khóa: thu hồi refresh token → access token hiện tại hết hạn là phải đăng nhập lại.
        if (sessionChanged)
        {
            await refreshTokens.RevokeAllForAccountAsync(id, clock.UtcNow);
        }

        return account.ToDto();
    }

    public async Task ResetPasswordAsync(int id, AdminResetPasswordRequest request)
    {
        var account = await FindAsync(id);
        account.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        account.UpdatedAt = clock.UtcNow;
        await accounts.UpdateAsync(account);
        await refreshTokens.RevokeAllForAccountAsync(id, clock.UtcNow);
    }

    public async Task<List<LookupItem>> LookupAsync(IReadOnlyCollection<string> roles)
    {
        var valid = roles.Where(Roles.IsValid).ToArray();
        if (valid.Length == 0)
        {
            throw new BusinessRuleException("Vai trò không hợp lệ.");
        }

        return (await accounts.ListActiveByRolesAsync(valid))
            .Select(account => new LookupItem(account.Id, account.FullName, $"{account.Role} · {account.Email}")).ToList();
    }

    private async Task<Account> FindAsync(int id) =>
        await accounts.GetByIdAsync(id) ?? throw new NotFoundException("Không tìm thấy tài khoản.");

    private static void ValidateRole(string role)
    {
        if (!Roles.IsValid(role))
        {
            throw new BusinessRuleException($"Vai trò không hợp lệ. Chọn một trong: {string.Join(", ", Roles.All)}.");
        }
    }
}
