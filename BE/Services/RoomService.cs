using System.Data;
using Microsoft.EntityFrameworkCore;
using NursingHomeManagement.Data;
using NursingHomeManagement.Models;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Services;

public sealed class RoomService(ApplicationDbContext database, CurrentUser user)
{
    public async Task<PagedList<Room>> ListAsync(string? search, int page)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        var query = database.Rooms.AsNoTracking().Include(room => room.Beds).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(room => room.Number.Contains(search));
        }
        return await PagedList<Room>.CreateAsync(query.OrderBy(room => room.Number), page);
    }

    public async Task<Room> GetAsync(int id)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        return await database.Rooms.AsNoTracking().Include(room => room.Beds)
            .SingleOrDefaultAsync(room => room.Id == id) ?? throw new KeyNotFoundException();
    }

    public async Task SaveAsync(int? id, RoomForm form)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var room = id.HasValue
            ? await database.Rooms.FindAsync(id.Value) ?? throw new KeyNotFoundException()
            : new Room();
        if (!form.IsActive && await database.Beds.AnyAsync(bed => bed.RoomId == id && bed.Status == BedStatus.Occupied))
        {
            throw new BusinessException("Phòng đang có cư dân lưu trú.");
        }
        room.Number = form.Number.Trim();
        room.Description = form.Description;
        room.IsActive = form.IsActive;
        if (!id.HasValue)
        {
            database.Rooms.Add(room);
        }
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        var room = await database.Rooms.Include(item => item.Beds).SingleOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException();
        if (room.Beds.Count > 0)
        {
            throw new BusinessException("Xóa các giường chưa sử dụng trước, hoặc ngừng sử dụng phòng để giữ lịch sử.");
        }
        database.Rooms.Remove(room);
        await database.SaveChangesAsync();
    }

    public async Task<Bed> BedAsync(int id)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        return await database.Beds.AsNoTracking().SingleOrDefaultAsync(bed => bed.Id == id)
            ?? throw new KeyNotFoundException();
    }

    public async Task SaveBedAsync(int? id, BedForm form)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (!await database.Rooms.AnyAsync(room => room.Id == form.RoomId))
        {
            throw new BusinessException("Không tìm thấy phòng.");
        }
        var bed = id.HasValue
            ? await database.Beds.FindAsync(id.Value) ?? throw new KeyNotFoundException()
            : new Bed { RoomId = form.RoomId };
        if (bed.RoomId != form.RoomId)
        {
            throw new BusinessException("Không chuyển giường vật lý sang phòng khác. Hãy tạo giường mới.");
        }
        if (bed.Status == BedStatus.Occupied && form.Maintenance)
        {
            throw new BusinessException("Không bảo trì giường đang sử dụng.");
        }
        bed.Number = form.Number.Trim();
        if (bed.Status != BedStatus.Occupied)
        {
            bed.Status = form.Maintenance ? BedStatus.Maintenance : BedStatus.Available;
        }
        if (!id.HasValue)
        {
            database.Beds.Add(bed);
        }
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task DeleteBedAsync(int id)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        var bed = await database.Beds.FindAsync(id) ?? throw new KeyNotFoundException();
        if (await database.BedAssignments.AnyAsync(assignment => assignment.BedId == id))
        {
            throw new BusinessException("Giường đã có lịch sử, không thể xóa. Có thể chuyển sang bảo trì.");
        }
        database.Beds.Remove(bed);
        await database.SaveChangesAsync();
    }

    public async Task AssignAsync(BedAssignmentForm form)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        // Serializable + filtered unique indexes protect both sides of a concurrent transfer.
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var resident = await database.Residents.FindAsync(form.ResidentId) ?? throw new KeyNotFoundException();
        var newBed = await database.Beds.Include(bed => bed.Room).SingleOrDefaultAsync(bed => bed.Id == form.BedId)
            ?? throw new KeyNotFoundException();
        if (resident.Status != ResidentStatus.Active || !newBed.Room.IsActive || newBed.Status != BedStatus.Available)
        {
            throw new BusinessException("Cư dân phải đang lưu trú và giường mới phải còn trống trong phòng đang sử dụng.");
        }
        var previous = await database.BedAssignments.Include(assignment => assignment.Bed)
            .SingleOrDefaultAsync(assignment => assignment.ResidentId == form.ResidentId && assignment.EndedAt == null);
        if (previous is not null)
        {
            previous.EndedAt = DateTimeOffset.UtcNow;
            previous.Bed.Status = BedStatus.Available;
            await database.SaveChangesAsync();
        }
        newBed.Status = BedStatus.Occupied;
        database.BedAssignments.Add(new BedAssignment
        {
            ResidentId = form.ResidentId,
            BedId = form.BedId,
            AssignedById = user.Id
        });
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task ReleaseAsync(int residentId)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var assignment = await database.BedAssignments.Include(item => item.Bed)
            .SingleOrDefaultAsync(item => item.ResidentId == residentId && item.EndedAt == null)
            ?? throw new BusinessException("Cư dân không có giường đang sử dụng.");
        assignment.EndedAt = DateTimeOffset.UtcNow;
        assignment.Bed.Status = BedStatus.Available;
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<PagedList<BedAssignment>> HistoryAsync(int? residentId, int page)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        var query = database.BedAssignments.AsNoTracking().Include(item => item.Resident)
            .Include(item => item.Bed).ThenInclude(bed => bed.Room).AsQueryable();
        if (residentId.HasValue)
        {
            query = query.Where(item => item.ResidentId == residentId);
        }
        return await PagedList<BedAssignment>.CreateAsync(query.OrderByDescending(item => item.StartedAt), page);
    }
}
