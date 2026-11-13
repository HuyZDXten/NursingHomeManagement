using System.Data;
using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.DataAccessObjects;

public sealed class RoomDAO : GenericDAO<Room>
{
    public static RoomDAO Instance { get; } = new();

    private RoomDAO()
    {
    }

    /// <summary>Phòng + giường + cư dân đang nằm (cho sơ đồ phòng).</summary>
    public async Task<List<Room>> ListAsync()
    {
        await using var context = CreateContext();
        return await context.Rooms.AsNoTracking()
            .Include(room => room.Beds)
            .ThenInclude(bed => bed.Assignments.Where(assignment => assignment.EndedAt == null))
            .ThenInclude(assignment => assignment.Resident)
            .OrderBy(room => room.Floor).ThenBy(room => room.Number)
            .ToListAsync();
    }

    public async Task<Room?> GetDetailAsync(int id)
    {
        await using var context = CreateContext();
        return await context.Rooms.AsNoTracking()
            .Include(room => room.Beds)
            .ThenInclude(bed => bed.Assignments.Where(assignment => assignment.EndedAt == null))
            .ThenInclude(assignment => assignment.Resident)
            .FirstOrDefaultAsync(room => room.Id == id);
    }

    public async Task<bool> NumberExistsAsync(string number, int? excludeId)
    {
        await using var context = CreateContext();
        return await context.Rooms.AnyAsync(room => room.Number == number && room.Id != excludeId);
    }

    public async Task<bool> HasOccupiedBedsAsync(int roomId)
    {
        await using var context = CreateContext();
        return await context.Beds.AnyAsync(bed => bed.RoomId == roomId && bed.Status == BedStatus.Occupied);
    }

    public async Task DeleteRoomAsync(int id)
    {
        await using var context = CreateContext();
        var room = await context.Rooms.Include(item => item.Beds).FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new NotFoundException("Không tìm thấy phòng.");
        if (room.Beds.Count > 0)
        {
            throw new BusinessRuleException("Xóa các giường chưa sử dụng trước, hoặc ngừng sử dụng phòng để giữ lịch sử.");
        }

        context.Rooms.Remove(room);
        await context.SaveChangesAsync();
    }

    // ---------------- Giường ----------------

    public async Task<Bed?> GetBedAsync(int id)
    {
        await using var context = CreateContext();
        return await context.Beds.AsNoTracking().Include(bed => bed.Room).FirstOrDefaultAsync(bed => bed.Id == id);
    }

    public async Task<bool> BedNumberExistsAsync(int roomId, string number, int? excludeId)
    {
        await using var context = CreateContext();
        return await context.Beds.AnyAsync(bed => bed.RoomId == roomId && bed.Number == number && bed.Id != excludeId);
    }

    public Task<Bed> AddBedAsync(Bed bed) => AddEntityAsync(bed);

    /// <summary>Đổi số giường / bảo trì. Không cho bảo trì giường đang có người.</summary>
    public async Task UpdateBedAsync(int id, string number, bool maintenance, DateTime now)
    {
        await using var context = CreateContext();
        var bed = await context.Beds.FindAsync(id) ?? throw new NotFoundException("Không tìm thấy giường.");
        if (bed.Status == BedStatus.Occupied && maintenance)
        {
            throw new BusinessRuleException("Không bảo trì giường đang sử dụng.");
        }

        bed.Number = number;
        if (bed.Status != BedStatus.Occupied)
        {
            bed.Status = maintenance ? BedStatus.Maintenance : BedStatus.Available;
        }

        bed.UpdatedAt = now;
        await context.SaveChangesAsync();
    }

    public async Task DeleteBedAsync(int id)
    {
        await using var context = CreateContext();
        var bed = await context.Beds.FindAsync(id) ?? throw new NotFoundException("Không tìm thấy giường.");
        if (await context.BedAssignments.AnyAsync(assignment => assignment.BedId == id))
        {
            throw new BusinessRuleException("Giường đã có lịch sử, không thể xóa. Có thể chuyển sang bảo trì.");
        }

        context.Beds.Remove(bed);
        await context.SaveChangesAsync();
    }

    // ---------------- Phân / chuyển / trả giường (UC-37, UC-38) ----------------

    /// <summary>
    /// Serializable + 2 filtered unique index (UX_BedAssignments_ActiveBed/ActiveResident) bảo vệ
    /// cả hai phía khi hai người cùng phân một giường.
    /// </summary>
    public async Task AssignAsync(int residentId, int bedId, int byAccountId, DateTime now)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var resident = await context.Residents.FindAsync(residentId) ?? throw new NotFoundException("Không tìm thấy cư dân.");
        var newBed = await context.Beds.Include(bed => bed.Room).FirstOrDefaultAsync(bed => bed.Id == bedId)
            ?? throw new NotFoundException("Không tìm thấy giường.");
        if (resident.Status != ResidentStatus.Active || !newBed.Room.IsActive || newBed.Status != BedStatus.Available)
        {
            throw new BusinessRuleException("Cư dân phải đang lưu trú và giường mới phải còn trống trong phòng đang sử dụng.");
        }

        var previous = await context.BedAssignments.Include(assignment => assignment.Bed)
            .FirstOrDefaultAsync(assignment => assignment.ResidentId == residentId && assignment.EndedAt == null);
        if (previous is not null)
        {
            previous.EndedAt = now;
            previous.Bed.Status = BedStatus.Available;
            previous.Bed.UpdatedAt = now;
            await context.SaveChangesAsync();
        }

        newBed.Status = BedStatus.Occupied;
        newBed.UpdatedAt = now;
        context.BedAssignments.Add(new BedAssignment
        {
            ResidentId = residentId,
            BedId = bedId,
            AssignedById = byAccountId,
            StartedAt = now
        });
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task ReleaseAsync(int residentId, DateTime now)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var assignment = await context.BedAssignments.Include(item => item.Bed)
            .FirstOrDefaultAsync(item => item.ResidentId == residentId && item.EndedAt == null)
            ?? throw new BusinessRuleException("Cư dân không có giường đang sử dụng.");
        assignment.EndedAt = now;
        assignment.Bed.Status = BedStatus.Available;
        assignment.Bed.UpdatedAt = now;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<List<BedAssignment>> HistoryAsync(int? residentId, int take)
    {
        await using var context = CreateContext();
        var query = context.BedAssignments.AsNoTracking()
            .Include(item => item.Resident)
            .Include(item => item.Bed).ThenInclude(bed => bed.Room)
            .Include(item => item.AssignedBy)
            .AsQueryable();
        if (residentId.HasValue)
        {
            query = query.Where(item => item.ResidentId == residentId);
        }

        return await query.OrderByDescending(item => item.StartedAt).Take(take).ToListAsync();
    }
}
