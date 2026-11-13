using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.DataAccessObjects;

public sealed class MedicationDAO : GenericDAO<MedicationSchedule>
{
    public static MedicationDAO Instance { get; } = new();

    private MedicationDAO()
    {
    }

    public async Task<List<MedicationSchedule>> ListSchedulesAsync(int? residentId, IReadOnlyCollection<int>? onlyResidentIds)
    {
        await using var context = CreateContext();
        var query = context.MedicationSchedules.AsNoTracking().Include(item => item.Resident).Include(item => item.EnteredBy).AsQueryable();
        if (residentId.HasValue)
        {
            query = query.Where(item => item.ResidentId == residentId);
        }

        if (onlyResidentIds is not null)
        {
            query = query.Where(item => onlyResidentIds.Contains(item.ResidentId));
        }

        return await query.OrderByDescending(item => item.IsActive).ThenBy(item => item.ScheduledTime).ToListAsync();
    }

    public async Task<MedicationSchedule?> GetScheduleAsync(int id)
    {
        await using var context = CreateContext();
        return await context.MedicationSchedules.AsNoTracking().Include(item => item.Resident).FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task StopAsync(int id, DateTime now)
    {
        await using var context = CreateContext();
        var schedule = await context.MedicationSchedules.FindAsync(id) ?? throw new NotFoundException("Không tìm thấy lịch thuốc.");
        schedule.IsActive = false;
        schedule.UpdatedAt = now;
        await context.SaveChangesAsync();
    }

    /// <summary>Lịch còn hiệu lực trong ngày, cư dân đang lưu trú (bảng phát thuốc UC-76).</summary>
    public async Task<List<MedicationSchedule>> ActiveForDateAsync(DateOnly date, IReadOnlyCollection<int>? onlyResidentIds)
    {
        await using var context = CreateContext();
        var query = context.MedicationSchedules.AsNoTracking().Include(item => item.Resident)
            .Where(item => item.IsActive && item.StartDate <= date && (item.EndDate == null || item.EndDate >= date) &&
                item.Resident.Status == ResidentStatus.Active);
        if (onlyResidentIds is not null)
        {
            query = query.Where(item => onlyResidentIds.Contains(item.ResidentId));
        }

        return await query.OrderBy(item => item.ScheduledTime).ThenBy(item => item.Id).ToListAsync();
    }

    public async Task<List<MedicationLog>> LogsBetweenAsync(IReadOnlyCollection<int> scheduleIds, DateTime startUtc, DateTime endUtc)
    {
        await using var context = CreateContext();
        return await context.MedicationLogs.AsNoTracking().Include(log => log.RecordedBy)
            .Where(log => scheduleIds.Contains(log.MedicationScheduleId) && log.ScheduledAt >= startUtc && log.ScheduledAt < endUtc)
            .ToListAsync();
    }

    public async Task<bool> LogExistsAsync(int scheduleId, DateTime scheduledAt)
    {
        await using var context = CreateContext();
        return await context.MedicationLogs.AnyAsync(log => log.MedicationScheduleId == scheduleId && log.ScheduledAt == scheduledAt);
    }

    /// <summary>Unique (MedicationScheduleId, ScheduledAt) chặn ghi trùng khi hai người bấm cùng lúc.</summary>
    public Task<MedicationLog> AddLogAsync(MedicationLog log) => AddEntityAsync(log);

    public async Task<List<MedicationLog>> ListLogsForResidentAsync(int residentId, int take)
    {
        await using var context = CreateContext();
        return await context.MedicationLogs.AsNoTracking()
            .Include(log => log.MedicationSchedule)
            .Include(log => log.RecordedBy)
            .Where(log => log.MedicationSchedule.ResidentId == residentId)
            .OrderByDescending(log => log.ScheduledAt).Take(take).ToListAsync();
    }
}
