using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;

namespace NursingHome.Repositories.Interfaces;

/// <summary>BL-V3: loại phòng & phụ thu, giữ giường, báo hỏng & sửa chữa.</summary>
public interface IRoomOpsRepository
{
    Task<List<RoomType>> ListTypesAsync(bool activeOnly);

    Task<RoomType?> GetTypeAsync(int id);

    Task<bool> TypeNameExistsAsync(string name, int? excludeId);

    Task<RoomType> AddTypeAsync(RoomType type);

    Task UpdateTypeAsync(RoomType type);

    Task<List<SurchargeStay>> SurchargeStaysAsync(int residentId, DateTime fromUtc, DateTime toUtc);

    Task<List<BedReservation>> ListReservationsAsync(bool activeOnly, DateTime now);

    Task<BedReservation?> GetReservationAsync(int id);

    Task<BedReservation> CreateReservationAsync(BedReservation reservation, DateTime now);

    Task<bool> ReleaseReservationAsync(int id, int byAccountId, DateTime now);

    Task<List<MaintenanceRequest>> ListMaintenanceAsync(MaintenanceStatus? status, int? roomId);

    Task<MaintenanceRequest?> GetMaintenanceAsync(int id);

    Task<MaintenanceRequest> CreateMaintenanceAsync(MaintenanceRequest request, DateTime now);

    Task UpdateMaintenanceAsync(int id, MaintenanceStatus status, string? assignedTo, string? note, int byAccountId, DateTime now);
}
