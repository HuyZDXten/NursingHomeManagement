using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class ResidentRepository : IResidentRepository
{
    private static ResidentDAO Dao => ResidentDAO.Instance;

    public Task<List<Resident>> ListAsync(IReadOnlyCollection<int>? onlyIds) => Dao.ListAsync(onlyIds);

    public Task<Resident?> GetByIdAsync(int id) => Dao.GetByIdAsync(id);

    public Task<Resident?> GetDetailAsync(int id) => Dao.GetDetailAsync(id);

    public Task<bool> CodeExistsAsync(string code, int? excludeId) => Dao.CodeExistsAsync(code, excludeId);

    public Task<ResidentStatus?> GetStatusAsync(int id) => Dao.GetStatusAsync(id);

    public Task<Resident> AddAsync(Resident resident) => Dao.AddAsync(resident);

    public Task UpdateAsync(Resident resident) => Dao.UpdateAsync(resident);

    public Task DischargeAsync(int id, int byAccountId, DateTime now, string reason) => Dao.DischargeAsync(id, byAccountId, now, reason);

    public Task<List<FamilyContact>> ListContactsAsync(int residentId) => Dao.ListContactsAsync(residentId);

    public Task<FamilyContact?> GetContactAsync(int id) => Dao.GetContactAsync(id);

    public Task<FamilyContact> AddContactAsync(FamilyContact contact) => Dao.AddContactAsync(contact);

    public Task UpdateContactAsync(FamilyContact contact) => Dao.UpdateContactAsync(contact);

    public Task DeleteContactAsync(FamilyContact contact) => Dao.DeleteContactAsync(contact);

    public Task<List<ResidentFamilyLink>> ListLinksAsync(int residentId) => Dao.ListLinksAsync(residentId);

    public Task<ResidentFamilyLink?> GetLinkAsync(int residentId, int familyAccountId) => Dao.GetLinkAsync(residentId, familyAccountId);

    public Task<ResidentFamilyLink?> GetLinkByIdAsync(int id) => Dao.GetLinkByIdAsync(id);

    public Task<List<ResidentFamilyLink>> ListLinksForFamilyAsync(int familyAccountId) => Dao.ListLinksForFamilyAsync(familyAccountId);

    public Task<List<int>> FamilyAccountIdsAsync(int residentId, bool requireBilling) => Dao.FamilyAccountIdsAsync(residentId, requireBilling);

    public Task<List<string>> FamilyEmailsAsync(int residentId, bool requireBilling) => Dao.FamilyEmailsAsync(residentId, requireBilling);

    public Task<ResidentFamilyLink> AddLinkAsync(ResidentFamilyLink link) => Dao.AddLinkAsync(link);

    public Task UpdateLinkAsync(ResidentFamilyLink link) => Dao.UpdateLinkAsync(link);

    public Task DeleteLinkAsync(ResidentFamilyLink link) => Dao.DeleteLinkAsync(link);
}

public class RoomRepository : IRoomRepository
{
    private static RoomDAO Dao => RoomDAO.Instance;

    public Task<List<Room>> ListAsync() => Dao.ListAsync();

    public Task<Room?> GetByIdAsync(int id) => Dao.GetByIdAsync(id);

    public Task<Room?> GetDetailAsync(int id) => Dao.GetDetailAsync(id);

    public Task<bool> NumberExistsAsync(string number, int? excludeId) => Dao.NumberExistsAsync(number, excludeId);

    public Task<bool> HasOccupiedBedsAsync(int roomId) => Dao.HasOccupiedBedsAsync(roomId);

    public Task<Room> AddAsync(Room room) => Dao.AddAsync(room);

    public Task UpdateAsync(Room room) => Dao.UpdateAsync(room);

    public Task DeleteRoomAsync(int id) => Dao.DeleteRoomAsync(id);

    public Task<Bed?> GetBedAsync(int id) => Dao.GetBedAsync(id);

    public Task<bool> BedNumberExistsAsync(int roomId, string number, int? excludeId) => Dao.BedNumberExistsAsync(roomId, number, excludeId);

    public Task<Bed> AddBedAsync(Bed bed) => Dao.AddBedAsync(bed);

    public Task UpdateBedAsync(int id, string number, bool maintenance, DateTime now) => Dao.UpdateBedAsync(id, number, maintenance, now);

    public Task DeleteBedAsync(int id) => Dao.DeleteBedAsync(id);

    public Task AssignAsync(int residentId, int bedId, int byAccountId, DateTime now) => Dao.AssignAsync(residentId, bedId, byAccountId, now);

    public Task ReleaseAsync(int residentId, DateTime now) => Dao.ReleaseAsync(residentId, now);

    public Task<List<BedAssignment>> HistoryAsync(int? residentId, int take) => Dao.HistoryAsync(residentId, take);
}
