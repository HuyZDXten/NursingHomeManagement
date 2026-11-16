using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class VisitRepository : IVisitRepository
{
    private static VisitDAO Dao => VisitDAO.Instance;

    public Task<Dictionary<TimeOnly, int>> CountGuestsAsync(DateOnly date) => Dao.CountGuestsAsync(date);
    public Task<VisitBooking> BookAsync(VisitBooking booking, int capacity) => Dao.BookAsync(booking, capacity);
    public Task<List<VisitBooking>> ListForFamilyAsync(int familyAccountId) => Dao.ListForFamilyAsync(familyAccountId);
    public Task<List<VisitBooking>> ListByDateAsync(DateOnly date, VisitStatus? status) => Dao.ListByDateAsync(date, status);
    public Task<VisitBooking?> GetDetailAsync(int id) => Dao.GetDetailAsync(id);
    public Task UpdateStatusAsync(int id, VisitStatus[] expectedStatuses, Action<VisitBooking> apply) =>
        Dao.UpdateStatusAsync(id, expectedStatuses, apply);
}
