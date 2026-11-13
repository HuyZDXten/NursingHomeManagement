using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class CareRepository : ICareRepository
{
    private static CareDAO Dao => CareDAO.Instance;

    public Task<List<CareTask>> ListTasksAsync(int? assignedToId, int? residentId, DateTime? fromUtc, DateTime? toUtc) =>
        Dao.ListTasksAsync(assignedToId, residentId, fromUtc, toUtc);

    public Task<CareTask?> GetTaskAsync(int id) => Dao.GetTaskAsync(id);

    public Task<CareTask> AddTaskAsync(CareTask task) => Dao.AddAsync(task);

    public Task UpdateStatusAsync(int id, byte[] rowVersion, CareTaskStatus status, CareRecord record, DateTime now) =>
        Dao.UpdateStatusAsync(id, rowVersion, status, record, now);

    public Task<List<CareRecord>> ListRecordsAsync(int taskId) => Dao.ListRecordsAsync(taskId);

    public Task<List<CareRecord>> ListRecordsForResidentAsync(int residentId, bool sharedOnly, int take) =>
        Dao.ListRecordsForResidentAsync(residentId, sharedOnly, take);

    public Task<bool> HasOpenTaskAsync(int residentId, int accountId) => Dao.HasOpenTaskAsync(residentId, accountId);

    public Task<List<int>> OpenTaskResidentIdsAsync(int accountId) => Dao.OpenTaskResidentIdsAsync(accountId);
}

public class HealthRepository : IHealthRepository
{
    private static HealthDAO Dao => HealthDAO.Instance;

    public Task<List<VitalSign>> ListAsync(int residentId, DateTime? fromUtc, DateTime? toUtc, bool sharedOnly, int take) =>
        Dao.ListAsync(residentId, fromUtc, toUtc, sharedOnly, take);

    public Task<VitalSign> AddAsync(VitalSign vitalSign) => Dao.AddAsync(vitalSign);
}

public class MedicationRepository : IMedicationRepository
{
    private static MedicationDAO Dao => MedicationDAO.Instance;

    public Task<List<MedicationSchedule>> ListSchedulesAsync(int? residentId, IReadOnlyCollection<int>? onlyResidentIds) =>
        Dao.ListSchedulesAsync(residentId, onlyResidentIds);

    public Task<MedicationSchedule?> GetScheduleAsync(int id) => Dao.GetScheduleAsync(id);

    public Task<MedicationSchedule> AddScheduleAsync(MedicationSchedule schedule) => Dao.AddAsync(schedule);

    public Task StopAsync(int id, DateTime now) => Dao.StopAsync(id, now);

    public Task<List<MedicationSchedule>> ActiveForDateAsync(DateOnly date, IReadOnlyCollection<int>? onlyResidentIds) =>
        Dao.ActiveForDateAsync(date, onlyResidentIds);

    public Task<List<MedicationLog>> LogsBetweenAsync(IReadOnlyCollection<int> scheduleIds, DateTime startUtc, DateTime endUtc) =>
        Dao.LogsBetweenAsync(scheduleIds, startUtc, endUtc);

    public Task<bool> LogExistsAsync(int scheduleId, DateTime scheduledAt) => Dao.LogExistsAsync(scheduleId, scheduledAt);

    public Task<MedicationLog> AddLogAsync(MedicationLog log) => Dao.AddLogAsync(log);

    public Task<List<MedicationLog>> ListLogsForResidentAsync(int residentId, int take) => Dao.ListLogsForResidentAsync(residentId, take);
}
