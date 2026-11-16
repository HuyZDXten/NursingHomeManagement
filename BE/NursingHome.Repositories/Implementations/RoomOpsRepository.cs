using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class RoomOpsRepository : IRoomOpsRepository
{
    private static RoomOpsDAO Dao => RoomOpsDAO.Instance;

    public Task<List<RoomType>> ListTypesAsync(bool activeOnly) => Dao.ListTypesAsync(activeOnly);

    public Task<RoomType?> GetTypeAsync(int id) => Dao.GetByIdAsync(id);

    public Task<bool> TypeNameExistsAsync(string name, int? excludeId) => Dao.TypeNameExistsAsync(name, excludeId);

    public Task<RoomType> AddTypeAsync(RoomType type) => Dao.AddAsync(type);

    public Task UpdateTypeAsync(RoomType type) => Dao.UpdateAsync(type);

    public Task<List<SurchargeStay>> SurchargeStaysAsync(int residentId, DateTime fromUtc, DateTime toUtc) =>
        Dao.SurchargeStaysAsync(residentId, fromUtc, toUtc);

    public Task<List<BedReservation>> ListReservationsAsync(bool activeOnly, DateTime now) => Dao.ListReservationsAsync(activeOnly, now);

    public Task<BedReservation?> GetReservationAsync(int id) => Dao.GetReservationAsync(id);

    public Task<BedReservation> CreateReservationAsync(BedReservation reservation, DateTime now) => Dao.CreateReservationAsync(reservation, now);

    public Task<bool> ReleaseReservationAsync(int id, int byAccountId, DateTime now) => Dao.ReleaseReservationAsync(id, byAccountId, now);

    public Task<List<MaintenanceRequest>> ListMaintenanceAsync(MaintenanceStatus? status, int? roomId) => Dao.ListMaintenanceAsync(status, roomId);

    public Task<MaintenanceRequest?> GetMaintenanceAsync(int id) => Dao.GetMaintenanceAsync(id);

    public Task<MaintenanceRequest> CreateMaintenanceAsync(MaintenanceRequest request, DateTime now) => Dao.CreateMaintenanceAsync(request, now);

    public Task UpdateMaintenanceAsync(int id, MaintenanceStatus status, string? assignedTo, string? note, int byAccountId, DateTime now) =>
        Dao.UpdateMaintenanceAsync(id, status, assignedTo, note, byAccountId, now);
}
