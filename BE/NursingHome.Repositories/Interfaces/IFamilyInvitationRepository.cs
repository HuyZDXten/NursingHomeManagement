using NursingHome.BusinessObjects.Entities;

namespace NursingHome.Repositories.Interfaces;

/// <summary>BL-V1, UC-08: mã mời kích hoạt tài khoản gia đình.</summary>
public interface IFamilyInvitationRepository
{
    Task<FamilyInvitation> CreateAsync(FamilyInvitation invitation, DateTime now);

    Task<List<FamilyInvitation>> ListAsync(int? residentId, int take);

    Task<FamilyInvitation?> GetAsync(int id);

    Task<FamilyInvitation?> GetByHashAsync(string codeHash);

    Task<bool> RevokeAsync(int id, DateTime now);

    Task<Account> AcceptAsync(int invitationId, Account account, DateTime now);
}
