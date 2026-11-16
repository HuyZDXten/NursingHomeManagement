using NursingHome.BusinessObjects.Entities;

namespace NursingHome.Repositories.Interfaces;

/// <summary>S3-H1: sự cố.</summary>
public interface IIncidentRepository
{
    Task<List<Incident>> ListAsync(int? residentId, IReadOnlyCollection<int>? onlyResidentIds,
        IncidentStatus? status, IncidentSeverity? severity, DateTime? fromUtc, DateTime? toUtc);

    Task<Incident?> GetDetailAsync(int id);

    Task<Incident> AddAsync(Incident incident);

    Task<List<Incident>> ListSharedForResidentAsync(int residentId, int take);

    Task UpdateStatusAsync(int id, byte[] rowVersion, Action<Incident> apply);

    Task<List<int>> FamilyCareViewerIdsAsync(int residentId);
}
