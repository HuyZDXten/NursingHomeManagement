using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;

namespace NursingHome.Repositories.Interfaces;

/// <summary>MODULE TÀI CHÍNH (Viên) — S3-V5 dịch vụ phát sinh.</summary>
public interface IExtraServiceRepository
{
    Task<List<ExtraService>> ListServicesAsync(bool activeOnly);

    Task<ExtraService?> GetServiceAsync(int id);

    Task<bool> ServiceNameExistsAsync(string name, int? excludeId);

    Task<ExtraService> AddServiceAsync(ExtraService service);

    Task UpdateServiceAsync(ExtraService service);

    Task<List<ServiceUsage>> ListUsagesAsync(IReadOnlyCollection<int>? residentIds, DateOnly from, DateOnly to, int take);

    Task<ServiceUsage?> GetUsageAsync(int id);

    Task<ServiceUsage> AddUsageAsync(ServiceUsage usage);

    Task DeleteUsageAsync(int id);
}

/// <summary>MODULE TÀI CHÍNH (Viên) — S3-V5 nhắc nợ.</summary>
public interface IReminderRepository
{
    Task<List<ReminderCandidate>> ListCandidatesAsync(DateOnly dueBy);

    Task<bool> TryMarkSentAsync(int invoiceId, ReminderStage stage, DateTime now);

    Task<List<InvoiceReminder>> ListForInvoiceAsync(int invoiceId);
}
