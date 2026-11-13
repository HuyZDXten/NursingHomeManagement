using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;

namespace NursingHome.Repositories.Interfaces;

public interface IResidentRepository
{
    Task<List<Resident>> ListAsync(IReadOnlyCollection<int>? onlyIds);

    Task<Resident?> GetByIdAsync(int id);

    Task<Resident?> GetDetailAsync(int id);

    Task<bool> CodeExistsAsync(string code, int? excludeId);

    Task<ResidentStatus?> GetStatusAsync(int id);

    Task<Resident> AddAsync(Resident resident);

    Task UpdateAsync(Resident resident);

    Task DischargeAsync(int id, int byAccountId, DateTime now, string reason);

    Task<List<FamilyContact>> ListContactsAsync(int residentId);

    Task<FamilyContact?> GetContactAsync(int id);

    Task<FamilyContact> AddContactAsync(FamilyContact contact);

    Task UpdateContactAsync(FamilyContact contact);

    Task DeleteContactAsync(FamilyContact contact);

    Task<List<ResidentFamilyLink>> ListLinksAsync(int residentId);

    Task<ResidentFamilyLink?> GetLinkAsync(int residentId, int familyAccountId);

    Task<ResidentFamilyLink?> GetLinkByIdAsync(int id);

    Task<List<ResidentFamilyLink>> ListLinksForFamilyAsync(int familyAccountId);

    Task<List<int>> FamilyAccountIdsAsync(int residentId, bool requireBilling);

    Task<ResidentFamilyLink> AddLinkAsync(ResidentFamilyLink link);

    Task UpdateLinkAsync(ResidentFamilyLink link);

    Task DeleteLinkAsync(ResidentFamilyLink link);
}

public interface IRoomRepository
{
    Task<List<Room>> ListAsync();

    Task<Room?> GetByIdAsync(int id);

    Task<Room?> GetDetailAsync(int id);

    Task<bool> NumberExistsAsync(string number, int? excludeId);

    Task<bool> HasOccupiedBedsAsync(int roomId);

    Task<Room> AddAsync(Room room);

    Task UpdateAsync(Room room);

    Task DeleteRoomAsync(int id);

    Task<Bed?> GetBedAsync(int id);

    Task<bool> BedNumberExistsAsync(int roomId, string number, int? excludeId);

    Task<Bed> AddBedAsync(Bed bed);

    Task UpdateBedAsync(int id, string number, bool maintenance, DateTime now);

    Task DeleteBedAsync(int id);

    Task AssignAsync(int residentId, int bedId, int byAccountId, DateTime now);

    Task ReleaseAsync(int residentId, DateTime now);

    Task<List<BedAssignment>> HistoryAsync(int? residentId, int take);
}
