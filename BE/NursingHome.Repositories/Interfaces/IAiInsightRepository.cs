using NursingHome.BusinessObjects.Entities;

namespace NursingHome.Repositories.Interfaces;

/// <summary>S4-H2: nhật ký hằng ngày AI &amp; xu hướng sinh hiệu.</summary>
public interface IAiInsightRepository
{
    Task<bool> TryAddAsync(AiInsight insight);
    Task<bool> HasAnyForDateAsync(InsightType type, DateOnly forDate);
    Task<List<AiInsight>> ListAsync(DateOnly? date, InsightType? type, InsightStatus? status);
    Task<AiInsight?> GetDetailAsync(long id);
    Task UpdateAsync(AiInsight insight);
    Task<List<AiInsight>> ListApprovedDigestsAsync(int residentId, DateOnly? date);
    Task<List<AiInsight>> ListVitalTrendsAsync(int residentId, int take);
    Task<List<(Resident Resident, bool IncludeCare, bool IncludeHealth)>> ActiveResidentsWithFamilyAsync();
    Task<List<Resident>> ActiveResidentsAsync();
    Task<List<VitalSign>> Last14DaysVitalsAsync(int residentId, DateTime sinceUtc);
}
