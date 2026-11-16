using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Care;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>S3-H4 (UC-51): kế hoạch chăm sóc và sinh việc tự động.</summary>
public class CarePlanService(
    ICarePlanRepository plans,
    IResidentRepository residents,
    IAccountRepository accounts,
    IStaffingRepository staffing,
    IResidentAccessService access,
    INotificationService notifications,
    FacilityClock clock) : ICarePlanService
{
    public async Task<List<CarePlanDto>> ListByResidentAsync(CurrentUser user, int residentId)
    {
        await access.RequireAsync(user, residentId, ResidentSection.Care);
        return (await plans.ListByResidentAsync(residentId)).Select(p => p.ToDto()).ToList();
    }

    public async Task<CarePlanDto> CreateAsync(CurrentUser user, SaveCarePlanRequest request)
    {
        if (await residents.GetStatusAsync(request.ResidentId) != ResidentStatus.Active)
        {
            throw new BusinessRuleException("Vui lòng chọn cư dân đang lưu trú.");
        }

        ValidateDates(request);
        var items = await BuildItemsAsync(request.Items);
        var saved = await plans.SaveAsync(null, new CarePlan
        {
            ResidentId = request.ResidentId,
            Title = request.Title.Trim(),
            Goals = request.Goals?.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true,
            CreatedById = user.AccountId,
            CreatedAt = clock.UtcNow
        }, items, clock.UtcNow);

        return saved.ToDto();
    }

    public async Task<CarePlanDto> UpdateAsync(CurrentUser user, int id, SaveCarePlanRequest request)
    {
        ValidateDates(request);
        var items = await BuildItemsAsync(request.Items);
        var saved = await plans.SaveAsync(id, new CarePlan
        {
            Title = request.Title.Trim(),
            Goals = request.Goals?.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate
        }, items, clock.UtcNow);

        return saved.ToDto();
    }

    public Task DeactivateAsync(int id) => plans.DeactivateAsync(id, clock.UtcNow);

    public async Task<GenerateTasksResultDto> GenerateForDatesAsync(int days = 2, int? onlyResidentId = null)
    {
        int clampedDays = Math.Clamp(days, 1, 7);
        int created = 0;
        int skipped = 0;
        var today = clock.Today;
        var assignmentCache = new Dictionary<int, List<ResidentCareAssignment>>();

        for (int offset = 0; offset < clampedDays; offset++)
        {
            var forDate = today.AddDays(offset);
            var items = await plans.ActiveItemsForDateAsync(forDate);
            foreach (var item in items)
            {
                if (onlyResidentId.HasValue && item.CarePlan.ResidentId != onlyResidentId.Value)
                {
                    continue;
                }

                if (!CareSchedule.IsScheduledForDate(item.DaysOfWeek, forDate))
                {
                    continue;
                }

                var dueAtUtc = clock.ToUtc(forDate, item.TimeOfDay);
                int? assigneeId = await ResolveAssigneeAsync(item, forDate, dueAtUtc, assignmentCache);
                if (!assigneeId.HasValue)
                {
                    skipped++;
                    await notifications.NotifyRolesAsync(
                        [Roles.Nurse],
                        NotificationType.Care,
                        "Mục kế hoạch chưa có người làm",
                        $"{item.CarePlan.Resident.FullName}: \"{item.Title}\" ngày {forDate:dd/MM} ({item.TimeOfDay:HH\\:mm}) chưa có người phụ trách.",
                        $"/staff/residents/{item.CarePlan.ResidentId}");
                    continue;
                }

                if (await plans.GenerateTaskAsync(item, forDate, dueAtUtc, assigneeId.Value, item.CarePlan.CreatedById, clock.UtcNow))
                {
                    created++;
                }
            }
        }

        return new GenerateTasksResultDto { CreatedCount = created, SkippedUnassignedCount = skipped };
    }

    private async Task<int?> ResolveAssigneeAsync(
        CarePlanItem item,
        DateOnly forDate,
        DateTime dueAtUtc,
        Dictionary<int, List<ResidentCareAssignment>> cache)
    {
        if (item.DefaultAssigneeId.HasValue &&
            await accounts.IsActiveInRolesAsync(item.DefaultAssigneeId.Value, Roles.CareAssignees))
        {
            return item.DefaultAssigneeId.Value;
        }

        int residentId = item.CarePlan.ResidentId;
        if (!cache.TryGetValue(residentId, out var assignments))
        {
            assignments = await staffing.ListCareAssignmentsAsync(residentId, null, activeOnly: true);
            cache[residentId] = assignments;
        }

        var activeOnDate = assignments.Where(a =>
            a.Account.IsActive &&
            a.StartDate <= forDate &&
            (!a.EndDate.HasValue || a.EndDate.Value >= forDate)).ToList();

        var shiftMatch = activeOnDate.FirstOrDefault(a =>
            a.Shift is not null &&
            (InWindow(dueAtUtc, StaffingRules.ShiftWindow(forDate, a.Shift)) ||
             InWindow(dueAtUtc, StaffingRules.ShiftWindow(forDate.AddDays(-1), a.Shift))));
        if (shiftMatch is not null)
        {
            return shiftMatch.AccountId;
        }

        return activeOnDate.FirstOrDefault(a => a.ShiftId is null)?.AccountId;
    }

    private static bool InWindow(DateTime utc, (DateTime StartUtc, DateTime EndUtc) window) =>
        utc >= window.StartUtc && utc < window.EndUtc;

    private static void ValidateDates(SaveCarePlanRequest request)
    {
        if (request.EndDate.HasValue && request.EndDate.Value < request.StartDate)
        {
            throw new BusinessRuleException("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.");
        }
    }

    private async Task<List<CarePlanItem>> BuildItemsAsync(List<SaveCarePlanItemRequest> requests)
    {
        var items = new List<CarePlanItem>(requests.Count);
        foreach (var r in requests)
        {
            if (r.DefaultAssigneeId.HasValue &&
                !await accounts.IsActiveInRolesAsync(r.DefaultAssigneeId.Value, Roles.CareAssignees))
            {
                throw new BusinessRuleException("Người làm mặc định phải là Nurse hoặc Caregiver đang hoạt động.");
            }

            items.Add(new CarePlanItem
            {
                Id = r.Id,
                Title = r.Title.Trim(),
                Instructions = r.Instructions?.Trim(),
                TimeOfDay = new TimeOnly(r.TimeOfDay.Hour, r.TimeOfDay.Minute),
                DaysOfWeek = r.DaysOfWeek,
                DefaultAssigneeId = r.DefaultAssigneeId,
                IsActive = true
            });
        }
        return items;
    }
}

/// <summary>S3-H4 (UC-56, UC-57): ghi nhận ADL và bữa ăn hằng ngày.</summary>
public class DailyLogService(
    IDailyLogRepository logs,
    IResidentRepository residents,
    IResidentAccessService access,
    FacilityClock clock) : IDailyLogService
{
    public async Task<List<DailyCareLogDto>> ListLogsAsync(CurrentUser user, int residentId, DateOnly date)
    {
        await access.RequireAsync(user, residentId, ResidentSection.Care);
        return (await logs.ListLogsAsync(residentId, date)).Select(l => l.ToDto()).ToList();
    }

    public async Task<DailyCareLogDto> AddLogAsync(CurrentUser user, int residentId, CreateDailyCareLogRequest request)
    {
        await ValidateResidentAndDateAsync(user, residentId, request.LogDate);
        if (!Enum.TryParse<DailyCareCategory>(request.Category, ignoreCase: true, out var category))
        {
            throw new BusinessRuleException("Hạng mục ADL không hợp lệ.");
        }

        if (!Enum.TryParse<DailyCareLevel>(request.Level, ignoreCase: true, out var level))
        {
            throw new BusinessRuleException("Mức độ ADL không hợp lệ.");
        }

        var saved = await logs.AddLogAsync(new DailyCareLog
        {
            ResidentId = residentId,
            LogDate = request.LogDate,
            Category = category,
            Level = level,
            Note = request.Note?.Trim(),
            RecordedById = user.AccountId,
            RecordedAt = clock.UtcNow,
            ShareWithFamily = request.ShareWithFamily
        });
        return saved.ToDto();
    }

    public async Task<List<MealLogDto>> ListMealsAsync(CurrentUser user, int residentId, DateOnly date)
    {
        await access.RequireAsync(user, residentId, ResidentSection.Care);
        return (await logs.ListMealsAsync(residentId, date)).Select(m => m.ToDto()).ToList();
    }

    public async Task<MealLogDto> UpsertMealAsync(CurrentUser user, int residentId, UpsertMealLogRequest request)
    {
        await ValidateResidentAndDateAsync(user, residentId, request.MealDate);
        if (!Enum.TryParse<MealType>(request.Meal, ignoreCase: true, out var meal))
        {
            throw new BusinessRuleException("Bữa ăn không hợp lệ (Breakfast, Lunch, Dinner, Snack).");
        }

        var saved = await logs.UpsertMealAsync(new MealLog
        {
            ResidentId = residentId,
            MealDate = request.MealDate,
            Meal = meal,
            IntakePercent = request.IntakePercent,
            FluidMl = request.FluidMl,
            Note = request.Note?.Trim(),
            RecordedById = user.AccountId,
            RecordedAt = clock.UtcNow,
            ShareWithFamily = request.ShareWithFamily
        });
        return saved.ToDto();
    }

    private async Task ValidateResidentAndDateAsync(CurrentUser user, int residentId, DateOnly date)
    {
        await access.RequireAsync(user, residentId, ResidentSection.Care);
        if (await residents.GetStatusAsync(residentId) != ResidentStatus.Active)
        {
            throw new BusinessRuleException("Cư dân phải đang lưu trú.");
        }

        var today = clock.Today;
        if (date > today)
        {
            throw new BusinessRuleException("Không thể ghi nhận cho ngày ở tương lai.");
        }

        if (!user.IsInRole(Roles.Admin, Roles.Nurse) && date < today.AddDays(-2))
        {
            throw new BusinessRuleException("Hộ lý chỉ được ghi bổ sung tối đa 2 ngày trước.");
        }
    }
}

/// <summary>S3-H4 (UC-61): gửi thông báo nhắc việc sắp đến hạn (30 phút) và quá hạn (60 phút).</summary>
public class TaskReminderService(
    ITaskReminderRepository reminders,
    INotificationService notifications,
    FacilityClock clock) : ITaskReminderService
{
    public async Task RunOnceAsync()
    {
        var now = clock.UtcNow;

        var dueSoon = await reminders.DueSoonWithoutReminderAsync(now, now.AddMinutes(30));
        foreach (var task in dueSoon)
        {
            var localDue = task.DueAt + FacilityClock.Offset;
            await notifications.NotifyAsync(
                [task.AssignedToId],
                NotificationType.Care,
                $"Sắp đến hạn: {task.Title}",
                $"{task.Resident.FullName} — hạn lúc {localDue:HH\\:mm dd/MM}.",
                "/staff/care-tasks");
            await reminders.MarkSentAsync(task.Id, ReminderKind.DueSoon, now);
        }

        var overdue = await reminders.OverdueWithoutReminderAsync(now.AddMinutes(-60));
        foreach (var task in overdue)
        {
            var localDue = task.DueAt + FacilityClock.Offset;
            await notifications.NotifyRolesAsync(
                [Roles.Nurse],
                NotificationType.Care,
                $"Việc quá hạn: {task.Title}",
                $"{task.Resident.FullName} (giao cho {task.AssignedTo.FullName}) — quá hạn từ {localDue:HH\\:mm dd/MM}.",
                "/staff/care-tasks");
            await reminders.MarkSentAsync(task.Id, ReminderKind.Overdue, now);
        }
    }
}
