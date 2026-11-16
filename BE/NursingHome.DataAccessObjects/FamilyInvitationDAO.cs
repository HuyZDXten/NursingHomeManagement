using System.Data;
using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.DataAccessObjects;

/// <summary>MODULE VẬN HÀNH (Viên) — BL-V1, UC-08: mã mời kích hoạt tài khoản gia đình.</summary>
public sealed class FamilyInvitationDAO : GenericDAO<FamilyInvitation>
{
    public static FamilyInvitationDAO Instance { get; } = new();

    private FamilyInvitationDAO()
    {
    }

    /// <summary>Tạo mã mới; mã còn chờ của cùng email + cư dân bị thu hồi (chỉ một mã có hiệu lực).</summary>
    public async Task<FamilyInvitation> CreateAsync(FamilyInvitation invitation, DateTime now)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await context.FamilyInvitations
            .Where(item => item.ResidentId == invitation.ResidentId && item.Email == invitation.Email
                && item.UsedAt == null && item.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokedAt, now));
        context.FamilyInvitations.Add(invitation);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return invitation;
    }

    public async Task<List<FamilyInvitation>> ListAsync(int? residentId, int take)
    {
        await using var context = CreateContext();
        var query = context.FamilyInvitations.AsNoTracking()
            .Include(item => item.Resident)
            .Include(item => item.CreatedBy)
            .Include(item => item.Account)
            .AsQueryable();
        if (residentId is { } id)
        {
            query = query.Where(item => item.ResidentId == id);
        }

        return await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Take(take).ToListAsync();
    }

    public async Task<FamilyInvitation?> GetAsync(int id)
    {
        await using var context = CreateContext();
        return await context.FamilyInvitations.AsNoTracking()
            .Include(item => item.Resident)
            .Include(item => item.CreatedBy)
            .Include(item => item.Account)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task<FamilyInvitation?> GetByHashAsync(string codeHash)
    {
        await using var context = CreateContext();
        return await context.FamilyInvitations.AsNoTracking()
            .Include(item => item.Resident)
            .FirstOrDefaultAsync(item => item.CodeHash == codeHash);
    }

    /// <summary>Thu hồi mã chưa dùng. Trả false nếu mã đã dùng hoặc đã thu hồi.</summary>
    public async Task<bool> RevokeAsync(int id, DateTime now)
    {
        await using var context = CreateContext();
        int changed = await context.FamilyInvitations
            .Where(item => item.Id == id && item.UsedAt == null && item.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokedAt, now));
        return changed == 1;
    }

    /// <summary>
    /// Kích hoạt trong MỘT transaction: kiểm mã còn hiệu lực, tạo tài khoản Family, liên kết cư dân theo quyền
    /// trong mã, đánh dấu mã đã dùng. Hai người dùng cùng mã đồng thời: chỉ một bên thành công.
    /// </summary>
    public async Task<Account> AcceptAsync(int invitationId, Account account, DateTime now)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var invitation = await context.FamilyInvitations.Include(item => item.Resident)
            .FirstOrDefaultAsync(item => item.Id == invitationId)
            ?? throw new NotFoundException("Mã mời không đúng.");
        if (invitation.UsedAt is not null)
        {
            throw new BusinessRuleException("Mã mời đã được dùng. Hãy đăng nhập bằng tài khoản đã tạo.");
        }

        if (invitation.RevokedAt is not null)
        {
            throw new BusinessRuleException("Mã mời đã bị thu hồi. Liên hệ lễ tân để nhận mã mới.");
        }

        if (invitation.ExpiresAt <= now)
        {
            throw new BusinessRuleException("Mã mời đã hết hạn. Liên hệ lễ tân để nhận mã mới.");
        }

        if (invitation.Resident.Status != ResidentStatus.Active)
        {
            throw new BusinessRuleException("Cư dân đã ngừng lưu trú, không kích hoạt được tài khoản.");
        }

        if (await context.Accounts.AnyAsync(item => item.Email == account.Email))
        {
            throw new ConflictException("Email này đã có tài khoản. Hãy đăng nhập, rồi nhờ lễ tân liên kết với cư dân.");
        }

        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        context.ResidentFamilyLinks.Add(new ResidentFamilyLink
        {
            ResidentId = invitation.ResidentId,
            FamilyAccountId = account.Id,
            CanViewHealth = invitation.CanViewHealth,
            CanViewCare = invitation.CanViewCare,
            CanViewMedication = invitation.CanViewMedication,
            CanViewBilling = invitation.CanViewBilling,
            CreatedAt = now
        });
        invitation.UsedAt = now;
        invitation.AccountId = account.Id;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return account;
    }
}
