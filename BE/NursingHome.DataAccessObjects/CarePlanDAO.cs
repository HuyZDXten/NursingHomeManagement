using System.Data;
using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.DataAccessObjects;

/// <summary>S3-H4 (UC-51): kế hoạch chăm sóc &amp; sinh việc tự động chống trùng.</summary>
public sealed class CarePlanDAO : GenericDAO<CarePlan>
{
    public static CarePlanDAO Instance { get; } = new();

    private CarePlanDAO()
    {
    }

    public async Task<List<CarePlan>> ListByResidentAsync(int residentId)
    {
        await using var context = CreateContext();
        return await context.CarePlans.AsNoTracking()
            .Include(p => p.CreatedBy)
            .Include(p => p.Items.Where(i => i.IsActive))
            .ThenInclude(i => i.DefaultAssignee)
            .Where(p => p.ResidentId == residentId)
            .OrderByDescending(p => p.IsActive)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<CarePlan?> GetDetailAsync(int id)
    {
        await using var context = CreateContext();
        return await context.CarePlans.AsNoTracking()
            .Include(p => p.CreatedBy)
            .Include(p => p.Items.Where(i => i.IsActive))
            .ThenInclude(i => i.DefaultAssignee)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<CarePlan> SaveAsync(int? id, CarePlan input, List<CarePlanItem> items, DateTime now)
    {
        await using var context = CreateContext();
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        CarePlan entity;
        if (!id.HasValue)
        {
            entity = input;
            entity.Items = items;
            context.CarePlans.Add(entity);
        }
        else
        {
            entity = await context.CarePlans.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id.Value)
                ?? throw new NotFoundException("Không tìm thấy kế hoạch chăm sóc.");
            entity.Title = input.Title;
            entity.Goals = input.Goals;
            entity.StartDate = input.StartDate;
            entity.EndDate = input.EndDate;
            entity.UpdatedAt = now;

            var incomingIds = items.Where(i => i.Id > 0).Select(i => i.Id).ToHashSet();
            foreach (var old in entity.Items.Where(i => i.IsActive && !incomingIds.Contains(i.Id)).ToList())
            {
                bool hasTasks = await context.GeneratedCareTasks.AnyAsync(g => g.CarePlanItemId == old.Id);
                if (hasTasks)
                {
                    old.IsActive = false;
                }
                else
                {
                    context.CarePlanItems.Remove(old);
                }
            }

            foreach (var item in items)
            {
                if (item.Id > 0)
                {
                    var existing = entity.Items.FirstOrDefault(i => i.Id == item.Id);
                    if (existing is not null)
                    {
                        existing.Title = item.Title;
                        existing.Instructions = item.Instructions;
                        existing.TimeOfDay = item.TimeOfDay;
                        existing.DaysOfWeek = item.DaysOfWeek;
                        existing.DefaultAssigneeId = item.DefaultAssigneeId;
                        existing.IsActive = true;
                        continue;
                    }
                }

                item.Id = 0;
                item.CarePlanId = entity.Id;
                item.IsActive = true;
                entity.Items.Add(item);
            }
        }

        await context.SaveChangesAsync();
        await tx.CommitAsync();
        return (await GetDetailAsync(entity.Id))!;
    }

    public async Task DeactivateAsync(int id, DateTime now)
    {
        await using var context = CreateContext();
        var plan = await context.CarePlans.FindAsync(id) ?? throw new NotFoundException("Không tìm thấy kế hoạch chăm sóc.");
        plan.IsActive = false;
        plan.UpdatedAt = now;
        await context.SaveChangesAsync();
    }

    public async Task<List<CarePlanItem>> ActiveItemsForDateAsync(DateOnly date)
    {
        await using var context = CreateContext();
        return await context.CarePlanItems.AsNoTracking()
            .Include(i => i.CarePlan)
            .ThenInclude(p => p.Resident)
            .Include(i => i.DefaultAssignee)
            .Where(i => i.IsActive &&
                        i.CarePlan.IsActive &&
                        i.CarePlan.StartDate <= date &&
                        (i.CarePlan.EndDate == null || i.CarePlan.EndDate >= date) &&
                        i.CarePlan.Resident.Status == ResidentStatus.Active)
            .ToListAsync();
    }

    /// <summary>
    /// Tạo CareTask và ghi bảng nối GeneratedCareTasks trong cùng một transaction.
    /// Trả false nếu mục này đã được sinh việc cho ngày <paramref name="forDate"/>.
    /// </summary>
    public async Task<bool> GenerateTaskAsync(CarePlanItem item, DateOnly forDate, DateTime dueAtUtc, int assigneeId, int createdById, DateTime now)
    {
        await using var context = CreateContext();
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        if (await context.GeneratedCareTasks.AnyAsync(g => g.CarePlanItemId == item.Id && g.ForDate == forDate))
        {
            return false;
        }

        var task = new CareTask
        {
            ResidentId = item.CarePlan.ResidentId,
            Title = item.Title,
            Instructions = item.Instructions,
            AssignedToId = assigneeId,
            CreatedById = createdById,
            DueAt = dueAtUtc,
            Status = CareTaskStatus.Pending,
            CreatedAt = now
        };
        context.CareTasks.Add(task);
        await context.SaveChangesAsync();

        context.GeneratedCareTasks.Add(new GeneratedCareTask
        {
            CarePlanItemId = item.Id,
            ForDate = forDate,
            CareTaskId = task.Id,
            CreatedAt = now
        });

        try
        {
            await context.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync();
            return false;
        }
    }
}

/// <summary>S3-H4 (UC-56, UC-57): ADL &amp; bữa ăn.</summary>
public sealed class DailyLogDAO : GenericDAO<DailyCareLog>
{
    public static DailyLogDAO Instance { get; } = new();

    private DailyLogDAO()
    {
    }

    public async Task<DailyCareLog> AddLogAsync(DailyCareLog log)
    {
        var saved = await AddAsync(log);
        await using var context = CreateContext();
        return await context.DailyCareLogs.AsNoTracking()
            .Include(l => l.RecordedBy)
            .FirstAsync(l => l.Id == saved.Id);
    }

    public async Task<List<DailyCareLog>> ListLogsAsync(int residentId, DateOnly date)
    {
        await using var context = CreateContext();
        return await context.DailyCareLogs.AsNoTracking()
            .Include(l => l.RecordedBy)
            .Where(l => l.ResidentId == residentId && l.LogDate == date)
            .OrderByDescending(l => l.RecordedAt)
            .ToListAsync();
    }

    public async Task<MealLog> UpsertMealAsync(MealLog meal)
    {
        await using var context = CreateContext();
        var existing = await context.MealLogs.FirstOrDefaultAsync(m =>
            m.ResidentId == meal.ResidentId && m.MealDate == meal.MealDate && m.Meal == meal.Meal);

        if (existing is null)
        {
            context.MealLogs.Add(meal);
        }
        else
        {
            existing.IntakePercent = meal.IntakePercent;
            existing.FluidMl = meal.FluidMl;
            existing.Note = meal.Note;
            existing.RecordedById = meal.RecordedById;
            existing.RecordedAt = meal.RecordedAt;
            existing.ShareWithFamily = meal.ShareWithFamily;
            meal = existing;
        }

        await context.SaveChangesAsync();
        return await context.MealLogs.AsNoTracking()
            .Include(m => m.RecordedBy)
            .FirstAsync(m => m.Id == meal.Id);
    }

    public async Task<List<MealLog>> ListMealsAsync(int residentId, DateOnly date)
    {
        await using var context = CreateContext();
        return await context.MealLogs.AsNoTracking()
            .Include(m => m.RecordedBy)
            .Where(m => m.ResidentId == residentId && m.MealDate == date)
            .OrderBy(m => m.Meal)
            .ToListAsync();
    }
}

/// <summary>S3-H4 (UC-61): truy vấn việc sắp đến hạn / quá hạn chưa gửi nhắc nhở.</summary>
public sealed class TaskReminderDAO : GenericDAO<TaskReminder>
{
    public static TaskReminderDAO Instance { get; } = new();

    private TaskReminderDAO()
    {
    }

    public async Task<List<CareTask>> DueSoonWithoutReminderAsync(DateTime fromUtc, DateTime toUtc)
    {
        await using var context = CreateContext();
        return await context.CareTasks.AsNoTracking()
            .Include(t => t.Resident)
            .Include(t => t.AssignedTo)
            .Where(t => t.Status == CareTaskStatus.Pending &&
                        t.DueAt >= fromUtc && t.DueAt <= toUtc &&
                        !context.TaskReminders.Any(r => r.CareTaskId == t.Id && r.Kind == ReminderKind.DueSoon))
            .ToListAsync();
    }

    public async Task<List<CareTask>> OverdueWithoutReminderAsync(DateTime cutoffUtc)
    {
        await using var context = CreateContext();
        return await context.CareTasks.AsNoTracking()
            .Include(t => t.Resident)
            .Include(t => t.AssignedTo)
            .Where(t => (t.Status == CareTaskStatus.Pending || t.Status == CareTaskStatus.InProgress) &&
                        t.DueAt <= cutoffUtc &&
                        !context.TaskReminders.Any(r => r.CareTaskId == t.Id && r.Kind == ReminderKind.Overdue))
            .ToListAsync();
    }

    public async Task MarkSentAsync(int taskId, ReminderKind kind, DateTime sentAtUtc)
    {
        await using var context = CreateContext();
        if (await context.TaskReminders.AnyAsync(r => r.CareTaskId == taskId && r.Kind == kind))
        {
            return;
        }

        context.TaskReminders.Add(new TaskReminder
        {
            CareTaskId = taskId,
            Kind = kind,
            SentAt = sentAtUtc
        });
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // đã đánh dấu ở lượt chạy khác
        }
    }
}
