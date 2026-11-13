using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;

namespace NursingHome.Repositories.Interfaces;

public interface ICareRepository
{
    Task<List<CareTask>> ListTasksAsync(int? assignedToId, int? residentId, DateTime? fromUtc, DateTime? toUtc);

    Task<CareTask?> GetTaskAsync(int id);

    Task<CareTask> AddTaskAsync(CareTask task);

    Task UpdateStatusAsync(int id, byte[] rowVersion, CareTaskStatus status, CareRecord record, DateTime now);

    Task<List<CareRecord>> ListRecordsAsync(int taskId);

    Task<List<CareRecord>> ListRecordsForResidentAsync(int residentId, bool sharedOnly, int take);

    Task<bool> HasOpenTaskAsync(int residentId, int accountId);

    Task<List<int>> OpenTaskResidentIdsAsync(int accountId);
}

public interface IHealthRepository
{
    Task<List<VitalSign>> ListAsync(int residentId, DateTime? fromUtc, DateTime? toUtc, bool sharedOnly, int take);

    Task<VitalSign> AddAsync(VitalSign vitalSign);
}

public interface IMedicationRepository
{
    Task<List<MedicationSchedule>> ListSchedulesAsync(int? residentId, IReadOnlyCollection<int>? onlyResidentIds);

    Task<MedicationSchedule?> GetScheduleAsync(int id);

    Task<MedicationSchedule> AddScheduleAsync(MedicationSchedule schedule);

    Task StopAsync(int id, DateTime now);

    Task<List<MedicationSchedule>> ActiveForDateAsync(DateOnly date, IReadOnlyCollection<int>? onlyResidentIds);

    Task<List<MedicationLog>> LogsBetweenAsync(IReadOnlyCollection<int> scheduleIds, DateTime startUtc, DateTime endUtc);

    Task<bool> LogExistsAsync(int scheduleId, DateTime scheduledAt);

    Task<MedicationLog> AddLogAsync(MedicationLog log);

    Task<List<MedicationLog>> ListLogsForResidentAsync(int residentId, int take);
}
