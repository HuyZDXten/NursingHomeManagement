using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Common;
using NursingHome.DTOs.Rooms;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>UC-34..UC-40: phòng, giường, phân/chuyển/trả giường, lịch sử, sơ đồ phòng.</summary>
public class RoomService(IRoomRepository rooms, FacilityClock clock) : IRoomService
{
    public async Task<List<RoomDto>> ListAsync() => (await rooms.ListAsync()).Select(room => room.ToDto()).ToList();

    public async Task<RoomDto> GetAsync(int id) =>
        (await rooms.GetDetailAsync(id) ?? throw new NotFoundException("Không tìm thấy phòng.")).ToDto();

    public async Task<RoomDto> SaveAsync(int? id, SaveRoomRequest request)
    {
        string number = request.Number.Trim();
        if (await rooms.NumberExistsAsync(number, id))
        {
            throw new ConflictException("Số phòng đã tồn tại.");
        }

        if (id.HasValue)
        {
            var room = await rooms.GetByIdAsync(id.Value) ?? throw new NotFoundException("Không tìm thấy phòng.");
            if (room.IsActive && !request.IsActive && await rooms.HasOccupiedBedsAsync(id.Value))
            {
                throw new BusinessRuleException("Phòng đang có cư dân lưu trú, không thể ngừng sử dụng.");
            }

            room.Number = number;
            room.Floor = request.Floor;
            room.Description = request.Description?.Trim();
            room.IsActive = request.IsActive;
            room.UpdatedAt = clock.UtcNow;
            await rooms.UpdateAsync(room);
            return await GetAsync(id.Value);
        }

        var created = await rooms.AddAsync(new Room
        {
            Number = number,
            Floor = request.Floor,
            Description = request.Description?.Trim(),
            IsActive = request.IsActive,
            CreatedAt = clock.UtcNow
        });
        return await GetAsync(created.Id);
    }

    public Task DeleteAsync(int id) => rooms.DeleteRoomAsync(id);

    public async Task<BedDto> SaveBedAsync(int roomId, int? bedId, SaveBedRequest request)
    {
        _ = await rooms.GetByIdAsync(roomId) ?? throw new NotFoundException("Không tìm thấy phòng.");
        string number = request.Number.Trim();
        if (await rooms.BedNumberExistsAsync(roomId, number, bedId))
        {
            throw new ConflictException("Số giường đã tồn tại trong phòng.");
        }

        if (bedId.HasValue)
        {
            var bed = await rooms.GetBedAsync(bedId.Value);
            if (bed is null || bed.RoomId != roomId)
            {
                throw new NotFoundException("Không tìm thấy giường trong phòng này.");
            }

            await rooms.UpdateBedAsync(bedId.Value, number, request.Maintenance, clock.UtcNow);
            return (await rooms.GetBedAsync(bedId.Value))!.ToDto();
        }

        var created = await rooms.AddBedAsync(new Bed
        {
            RoomId = roomId,
            Number = number,
            Status = request.Maintenance ? BusinessObjects.Enums.BedStatus.Maintenance : BusinessObjects.Enums.BedStatus.Available,
            CreatedAt = clock.UtcNow
        });
        return created.ToDto();
    }

    public Task DeleteBedAsync(int bedId) => rooms.DeleteBedAsync(bedId);

    public Task AssignAsync(CurrentUser user, AssignBedRequest request) =>
        rooms.AssignAsync(request.ResidentId, request.BedId, user.AccountId, clock.UtcNow);

    public Task ReleaseAsync(int residentId) => rooms.ReleaseAsync(residentId, clock.UtcNow);

    public async Task<List<BedAssignmentDto>> HistoryAsync(int? residentId) =>
        (await rooms.HistoryAsync(residentId, 200)).Select(item => item.ToDto()).ToList();
}
