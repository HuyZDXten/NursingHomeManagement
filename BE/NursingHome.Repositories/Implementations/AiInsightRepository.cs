using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class AiInsightRepository : IAiInsightRepository
{
    private static AiInsightDAO Dao => AiInsightDAO.Instance;

    public Task<bool> TryAddAsync(AiInsight insight) => Dao.TryAddAsync(insight);
    public Task<bool> HasAnyForDateAsync(InsightType type, DateOnly forDate) => Dao.HasAnyForDateAsync(type, forDate);
    public Task<List<AiInsight>> ListAsync(DateOnly? date, InsightType? type, InsightStatus? status) =>
        Dao.ListAsync(date, type, status);
    public Task<AiInsight?> GetDetailAsync(long id) => Dao.GetDetailAsync(id);
    public Task UpdateAsync(AiInsight insight) => Dao.UpdateAsync(insight);
    public Task<List<AiInsight>> ListApprovedDigestsAsync(int residentId, DateOnly? date) =>
        Dao.ListApprovedDigestsAsync(residentId, date);
    public Task<List<AiInsight>> ListVitalTrendsAsync(int residentId, int take) => Dao.ListVitalTrendsAsync(residentId, take);
    public Task<List<(Resident Resident, bool IncludeCare, bool IncludeHealth)>> ActiveResidentsWithFamilyAsync() =>
        Dao.ActiveResidentsWithFamilyAsync();
    public Task<List<Resident>> ActiveResidentsAsync() => Dao.ActiveResidentsAsync();
    public Task<List<VitalSign>> Last14DaysVitalsAsync(int residentId, DateTime sinceUtc) =>
        Dao.Last14DaysVitalsAsync(residentId, sinceUtc);
}
