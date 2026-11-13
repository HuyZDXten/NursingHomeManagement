using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.DataAccessObjects;

public sealed class CareDAO : GenericDAO<CareTask>
{
    public static CareDAO Instance { get; } = new();

    private CareDAO()
    {
    }

    public async Task<List<CareTask>> ListTasksAsync(int? assignedToId, int? residentId, DateTime? fromUtc, DateTime? toUtc)
    {
        await using var context = CreateContext();
        var query = context.CareTasks.AsNoTracking().Include(task => task.Resident).Include(task => task.AssignedTo).AsQueryable();
        if (assignedToId.HasValue)
        {
            query = query.Where(task => task.AssignedToId == assignedToId);
        }

        if (residentId.HasValue)
        {
            query = query.Where(task => task.ResidentId == residentId);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(task => task.DueAt >= fromUtc);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(task => task.DueAt < toUtc);
        }

        return await query.OrderBy(task => task.DueAt).ToListAsync();
    }

    public async Task<CareTask?> GetTaskAsync(int id)
    {
        await using var context = CreateContext();
        return await context.CareTasks.AsNoTracking().Include(task => task.Resident).Include(task => task.AssignedTo)
            .FirstOrDefaultAsync(task => task.Id == id);
    }

    /// <summary>
    /// Đổi trạng thái + thêm CareRecord trong một SaveChanges. RowVersion client gửi lên là điều kiện:
    /// nếu người khác đã cập nhật trước, EF ném DbUpdateConcurrencyException (API trả 409).
    /// </summary>
    public async Task UpdateStatusAsync(int id, byte[] rowVersion, CareTaskStatus status, CareRecord record, DateTime now)
    {
        await using var context = CreateContext();
        var task = await context.CareTasks.FindAsync(id) ?? throw new NotFoundException("Không tìm thấy công việc.");
        if (task.Status is CareTaskStatus.Completed or CareTaskStatus.Cancelled)
        {
            throw new BusinessRuleException("Công việc đã kết thúc, không thể sửa lịch sử.");
        }

        context.Entry(task).Property(item => item.RowVersion).OriginalValue = rowVersion;
        task.Status = status;
        task.UpdatedAt = now;
        record.CareTaskId = id;
        context.CareRecords.Add(record);
        await context.SaveChangesAsync();
    }

    public async Task<List<CareRecord>> ListRecordsAsync(int taskId)
    {
        await using var context = CreateContext();
        return await context.CareRecords.AsNoTracking().Include(record => record.RecordedBy)
            .Where(record => record.CareTaskId == taskId)
            .OrderByDescending(record => record.PerformedAt).ToListAsync();
    }

    public async Task<List<CareRecord>> ListRecordsForResidentAsync(int residentId, bool sharedOnly, int take)
    {
        await using var context = CreateContext();
        return await context.CareRecords.AsNoTracking()
            .Include(record => record.CareTask)
            .Include(record => record.RecordedBy)
            .Where(record => record.CareTask.ResidentId == residentId && (!sharedOnly || record.ShareWithFamily))
            .OrderByDescending(record => record.PerformedAt).Take(take).ToListAsync();
    }

    public async Task<bool> HasOpenTaskAsync(int residentId, int accountId)
    {
        await using var context = CreateContext();
        return await context.CareTasks.AnyAsync(task => task.ResidentId == residentId && task.AssignedToId == accountId &&
            (task.Status == CareTaskStatus.Pending || task.Status == CareTaskStatus.InProgress));
    }

    /// <summary>Cư dân mà nhân viên đang có việc Pending/InProgress (phạm vi xem của Caregiver).</summary>
    public async Task<List<int>> OpenTaskResidentIdsAsync(int accountId)
    {
        await using var context = CreateContext();
        return await context.CareTasks
            .Where(task => task.AssignedToId == accountId &&
                (task.Status == CareTaskStatus.Pending || task.Status == CareTaskStatus.InProgress))
            .Select(task => task.ResidentId).Distinct().ToListAsync();
    }
}

public sealed class HealthDAO : GenericDAO<VitalSign>
{
    public static HealthDAO Instance { get; } = new();

    private HealthDAO()
    {
    }

    public async Task<List<VitalSign>> ListAsync(int residentId, DateTime? fromUtc, DateTime? toUtc, bool sharedOnly, int take)
    {
        await using var context = CreateContext();
        var query = context.VitalSigns.AsNoTracking().Include(item => item.RecordedBy)
            .Where(item => item.ResidentId == residentId && (!sharedOnly || item.ShareWithFamily));
        if (fromUtc.HasValue)
        {
            query = query.Where(item => item.RecordedAt >= fromUtc);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(item => item.RecordedAt < toUtc);
        }

        return await query.OrderByDescending(item => item.RecordedAt).Take(take).ToListAsync();
    }
}
