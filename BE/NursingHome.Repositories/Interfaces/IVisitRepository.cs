using NursingHome.BusinessObjects.Entities;

namespace NursingHome.Repositories.Interfaces;

/// <summary>S3-H3: đặt &amp; duyệt lịch thăm.</summary>
public interface IVisitRepository
{
    Task<Dictionary<TimeOnly, int>> CountGuestsAsync(DateOnly date);
    Task<VisitBooking> BookAsync(VisitBooking booking, int capacity);
    Task<List<VisitBooking>> ListForFamilyAsync(int familyAccountId);
    Task<List<VisitBooking>> ListByDateAsync(DateOnly date, VisitStatus? status);
    Task<VisitBooking?> GetDetailAsync(int id);
    Task UpdateStatusAsync(int id, VisitStatus[] expectedStatuses, Action<VisitBooking> apply);
}
