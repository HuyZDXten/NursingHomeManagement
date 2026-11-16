using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class CarePlanRepository : ICarePlanRepository
{
    private static CarePlanDAO Dao => CarePlanDAO.Instance;

    public Task<List<CarePlan>> ListByResidentAsync(int residentId) => Dao.ListByResidentAsync(residentId);
    public Task<CarePlan?> GetDetailAsync(int id) => Dao.GetDetailAsync(id);
    public Task<CarePlan> SaveAsync(int? id, CarePlan input, List<CarePlanItem> items, DateTime now) =>
        Dao.SaveAsync(id, input, items, now);
    public Task DeactivateAsync(int id, DateTime now) => Dao.DeactivateAsync(id, now);
    public Task<List<CarePlanItem>> ActiveItemsForDateAsync(DateOnly date) => Dao.ActiveItemsForDateAsync(date);
    public Task<bool> GenerateTaskAsync(CarePlanItem item, DateOnly forDate, DateTime dueAtUtc, int assigneeId, int createdById, DateTime now) =>
        Dao.GenerateTaskAsync(item, forDate, dueAtUtc, assigneeId, createdById, now);
}

public class DailyLogRepository : IDailyLogRepository
{
    private static DailyLogDAO Dao => DailyLogDAO.Instance;

    public Task<DailyCareLog> AddLogAsync(DailyCareLog log) => Dao.AddLogAsync(log);
    public Task<List<DailyCareLog>> ListLogsAsync(int residentId, DateOnly date) => Dao.ListLogsAsync(residentId, date);
    public Task<MealLog> UpsertMealAsync(MealLog meal) => Dao.UpsertMealAsync(meal);
    public Task<List<MealLog>> ListMealsAsync(int residentId, DateOnly date) => Dao.ListMealsAsync(residentId, date);
}

public class TaskReminderRepository : ITaskReminderRepository
{
    private static TaskReminderDAO Dao => TaskReminderDAO.Instance;

    public Task<List<CareTask>> DueSoonWithoutReminderAsync(DateTime fromUtc, DateTime toUtc) =>
        Dao.DueSoonWithoutReminderAsync(fromUtc, toUtc);
    public Task<List<CareTask>> OverdueWithoutReminderAsync(DateTime cutoffUtc) =>
        Dao.OverdueWithoutReminderAsync(cutoffUtc);
    public Task MarkSentAsync(int taskId, ReminderKind kind, DateTime sentAtUtc) =>
        Dao.MarkSentAsync(taskId, kind, sentAtUtc);
}
