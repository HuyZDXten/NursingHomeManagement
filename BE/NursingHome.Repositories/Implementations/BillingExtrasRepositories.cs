using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class ExtraServiceRepository : IExtraServiceRepository
{
    private static ExtraServiceDAO Dao => ExtraServiceDAO.Instance;

    public Task<List<ExtraService>> ListServicesAsync(bool activeOnly) => Dao.ListServicesAsync(activeOnly);

    public Task<ExtraService?> GetServiceAsync(int id) => Dao.GetServiceAsync(id);

    public Task<bool> ServiceNameExistsAsync(string name, int? excludeId) => Dao.ServiceNameExistsAsync(name, excludeId);

    public Task<ExtraService> AddServiceAsync(ExtraService service) => Dao.AddServiceAsync(service);

    public Task UpdateServiceAsync(ExtraService service) => Dao.UpdateServiceAsync(service);

    public Task<List<ServiceUsage>> ListUsagesAsync(IReadOnlyCollection<int>? residentIds, DateOnly from, DateOnly to, int take) =>
        Dao.ListUsagesAsync(residentIds, from, to, take);

    public Task<ServiceUsage?> GetUsageAsync(int id) => Dao.GetUsageAsync(id);

    public Task<ServiceUsage> AddUsageAsync(ServiceUsage usage) => Dao.AddUsageAsync(usage);

    public Task DeleteUsageAsync(int id) => Dao.DeleteUsageAsync(id);
}

public class ReminderRepository : IReminderRepository
{
    private static ReminderDAO Dao => ReminderDAO.Instance;

    public Task<List<ReminderCandidate>> ListCandidatesAsync(DateOnly dueBy) => Dao.ListCandidatesAsync(dueBy);

    public Task<bool> TryMarkSentAsync(int invoiceId, ReminderStage stage, DateTime now) => Dao.TryMarkSentAsync(invoiceId, stage, now);

    public Task<List<InvoiceReminder>> ListForInvoiceAsync(int invoiceId) => Dao.ListForInvoiceAsync(invoiceId);
}
