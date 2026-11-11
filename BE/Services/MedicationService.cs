using System.Data;
using Microsoft.EntityFrameworkCore;
using NursingHomeManagement.Data;
using NursingHomeManagement.Models;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Services;

public sealed class MedicationService(
    ApplicationDbContext database,
    CurrentUser user,
    ResidentAccessService access,
    FacilityClock clock)
{
    public async Task<PagedList<MedicationSchedule>> ListAsync(int? residentId, int page)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Caregiver);
        var query = AllowedSchedules().AsNoTracking().Include(schedule => schedule.Resident);
        IQueryable<MedicationSchedule> filtered = query;
        if (residentId.HasValue)
        {
            await access.RequireAsync(user, residentId.Value, ResidentSection.Medication);
            filtered = filtered.Where(schedule => schedule.ResidentId == residentId);
        }
        return await PagedList<MedicationSchedule>.CreateAsync(filtered.OrderByDescending(schedule => schedule.CreatedAt), page);
    }

    private IQueryable<MedicationSchedule> AllowedSchedules()
    {
        var query = database.MedicationSchedules.AsQueryable();
        if (user.IsInRole(Roles.Caregiver))
        {
            query = query.Where(schedule => database.CareTasks.Any(task => task.ResidentId == schedule.ResidentId &&
                task.AssignedToId == user.Id && (task.Status == CareTaskStatus.Pending || task.Status == CareTaskStatus.InProgress)));
        }
        return query;
    }

    public async Task<PagedList<MedicationDay>> TodayAsync(DateOnly date, int page = 1)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Caregiver);
        var scheduleQuery = AllowedSchedules().AsNoTracking().Include(schedule => schedule.Resident)
            .Where(schedule => schedule.IsActive && schedule.StartDate <= date &&
                (schedule.EndDate == null || schedule.EndDate >= date) && schedule.Resident.Status == ResidentStatus.Active)
            .OrderBy(schedule => schedule.ScheduledTime).ThenBy(schedule => schedule.Id);
        var schedulePage = await PagedList<MedicationSchedule>.CreateAsync(scheduleQuery, page);
        var schedules = schedulePage.Items;
        var ids = schedules.Select(schedule => schedule.Id).ToList();
        var start = clock.StartOfDay(date);
        var end = start.AddDays(1);
        var logs = await database.MedicationLogs.AsNoTracking().Where(log => ids.Contains(log.MedicationScheduleId) &&
            log.ScheduledAt >= start && log.ScheduledAt < end).ToListAsync();
        return new PagedList<MedicationDay>
        {
            Items = schedules.Select(schedule => new MedicationDay(schedule, date,
                logs.SingleOrDefault(log => log.MedicationScheduleId == schedule.Id))).ToList(),
            Page = schedulePage.Page,
            TotalPages = schedulePage.TotalPages,
            TotalCount = schedulePage.TotalCount
        };
    }

    public async Task CreateAsync(MedicationForm form)
    {
        await user.RequireRoleAsync(Roles.Admin);
        if (!await database.Residents.AnyAsync(resident => resident.Id == form.ResidentId && resident.Status == ResidentStatus.Active))
        {
            throw new BusinessException("Cư dân phải đang lưu trú.");
        }
        if (form.EndDate < form.StartDate || string.IsNullOrWhiteSpace(form.ApprovalReference))
        {
            throw new BusinessException("Ngày kết thúc hoặc tham chiếu phê duyệt không hợp lệ.");
        }
        database.MedicationSchedules.Add(new MedicationSchedule
        {
            ResidentId = form.ResidentId,
            MedicationName = form.MedicationName.Trim(),
            Dose = form.Dose.Trim(),
            Route = form.Route.Trim(),
            StartDate = form.StartDate,
            EndDate = form.EndDate,
            ScheduledTime = form.ScheduledTime,
            ApprovalReference = form.ApprovalReference.Trim(),
            Instructions = form.Instructions,
            EnteredById = user.Id
        });
        await database.SaveChangesAsync();
    }

    public async Task StopAsync(int id)
    {
        await user.RequireRoleAsync(Roles.Admin);
        var schedule = await database.MedicationSchedules.FindAsync(id) ?? throw new KeyNotFoundException();
        schedule.IsActive = false;
        await database.SaveChangesAsync();
    }

    public async Task RecordAsync(MedicationLogForm form)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Caregiver);
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var schedule = await database.MedicationSchedules.Include(item => item.Resident)
            .SingleOrDefaultAsync(item => item.Id == form.ScheduleId) ?? throw new KeyNotFoundException();
        await access.RequireAsync(user, schedule.ResidentId, ResidentSection.Medication);
        var scheduledAt = clock.At(form.Date, schedule.ScheduledTime);
        if (!schedule.IsActive || schedule.Resident.Status != ResidentStatus.Active || form.Date < schedule.StartDate ||
            form.Date > schedule.EndDate || scheduledAt > DateTimeOffset.UtcNow)
        {
            throw new BusinessException("Không thể ghi nhận lịch đã dừng, ngoài thời hạn hoặc chưa đến giờ dùng.");
        }
        if (form.Status is not (MedicationStatus.Taken or MedicationStatus.Missed or MedicationStatus.Refused))
        {
            throw new BusinessException("Chọn Taken, Missed hoặc Refused.");
        }
        if (form.Status != MedicationStatus.Taken && string.IsNullOrWhiteSpace(form.Notes))
        {
            throw new BusinessException("Cần ghi lý do chưa dùng hoặc từ chối.");
        }
        if (await database.MedicationLogs.AnyAsync(log => log.MedicationScheduleId == form.ScheduleId && log.ScheduledAt == scheduledAt))
        {
            throw new BusinessException("Lần dùng thuốc này đã được ghi nhận, không ghi đè lịch sử.");
        }
        database.MedicationLogs.Add(new MedicationLog
        {
            MedicationScheduleId = form.ScheduleId,
            ScheduledAt = scheduledAt,
            RecordedAt = DateTimeOffset.UtcNow,
            RecordedById = user.Id,
            Status = form.Status,
            Notes = form.Notes
        });
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<PagedList<MedicationLog>> HistoryAsync(int residentId, int page)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Caregiver);
        await access.RequireAsync(user, residentId, ResidentSection.Medication);
        var query = database.MedicationLogs.AsNoTracking().Include(log => log.MedicationSchedule).Include(log => log.RecordedBy)
            .Where(log => log.MedicationSchedule.ResidentId == residentId).OrderByDescending(log => log.ScheduledAt);
        return await PagedList<MedicationLog>.CreateAsync(query, page);
    }
}
