using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class FamilyInvitationRepository : IFamilyInvitationRepository
{
    private static FamilyInvitationDAO Dao => FamilyInvitationDAO.Instance;

    public Task<FamilyInvitation> CreateAsync(FamilyInvitation invitation, DateTime now) => Dao.CreateAsync(invitation, now);

    public Task<List<FamilyInvitation>> ListAsync(int? residentId, int take) => Dao.ListAsync(residentId, take);

    public Task<FamilyInvitation?> GetAsync(int id) => Dao.GetAsync(id);

    public Task<FamilyInvitation?> GetByHashAsync(string codeHash) => Dao.GetByHashAsync(codeHash);

    public Task<bool> RevokeAsync(int id, DateTime now) => Dao.RevokeAsync(id, now);

    public Task<Account> AcceptAsync(int invitationId, Account account, DateTime now) => Dao.AcceptAsync(invitationId, account, now);
}
