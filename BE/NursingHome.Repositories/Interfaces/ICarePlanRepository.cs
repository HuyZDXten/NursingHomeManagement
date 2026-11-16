using NursingHome.BusinessObjects.Entities;

namespace NursingHome.Repositories.Interfaces;

/// <summary>S3-H4: kế hoạch chăm sóc, nhật ký ADL, bữa ăn, nhắc việc.</summary>
public interface ICarePlanRepository
{
    Task<List<CarePlan>> ListByResidentAsync(int residentId);
    Task<CarePlan?> GetDetailAsync(int id);
    Task<CarePlan> SaveAsync(int? id, CarePlan input, List<CarePlanItem> items, DateTime now);
    Task DeactivateAsync(int id, DateTime now);
    Task<List<CarePlanItem>> ActiveItemsForDateAsync(DateOnly date);
    Task<bool> GenerateTaskAsync(CarePlanItem item, DateOnly forDate, DateTime dueAtUtc, int assigneeId, int createdById, DateTime now);
}

public interface IDailyLogRepository
{
    Task<DailyCareLog> AddLogAsync(DailyCareLog log);
    Task<List<DailyCareLog>> ListLogsAsync(int residentId, DateOnly date);
    Task<MealLog> UpsertMealAsync(MealLog meal);
    Task<List<MealLog>> ListMealsAsync(int residentId, DateOnly date);
}

public interface ITaskReminderRepository
{
    Task<List<CareTask>> DueSoonWithoutReminderAsync(DateTime fromUtc, DateTime toUtc);
    Task<List<CareTask>> OverdueWithoutReminderAsync(DateTime cutoffUtc);
    Task MarkSentAsync(int taskId, ReminderKind kind, DateTime sentAtUtc);
}
