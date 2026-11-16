using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class IncidentRepository : IIncidentRepository
{
    private static IncidentDAO Dao => IncidentDAO.Instance;

    public Task<List<Incident>> ListAsync(int? residentId, IReadOnlyCollection<int>? onlyResidentIds,
        IncidentStatus? status, IncidentSeverity? severity, DateTime? fromUtc, DateTime? toUtc) =>
        Dao.ListAsync(residentId, onlyResidentIds, status, severity, fromUtc, toUtc);

    public Task<Incident?> GetDetailAsync(int id) => Dao.GetDetailAsync(id);

    public Task<Incident> AddAsync(Incident incident) => Dao.AddAsync(incident);

    public Task<List<Incident>> ListSharedForResidentAsync(int residentId, int take) => Dao.ListSharedForResidentAsync(residentId, take);

    public Task UpdateStatusAsync(int id, byte[] rowVersion, Action<Incident> apply) => Dao.UpdateStatusAsync(id, rowVersion, apply);

    public Task<List<int>> FamilyCareViewerIdsAsync(int residentId) => Dao.FamilyCareViewerIdsAsync(residentId);
}
