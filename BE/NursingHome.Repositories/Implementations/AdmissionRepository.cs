using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class AdmissionRepository : IAdmissionRepository
{
    private static AdmissionDAO Dao => AdmissionDAO.Instance;

    public Task<List<Inquiry>> ListInquiriesAsync() => Dao.ListInquiriesAsync();

    public Task<Inquiry?> GetInquiryDetailAsync(int id) => Dao.GetInquiryDetailAsync(id);

    public Task<Inquiry?> GetInquiryAsync(int id) => Dao.GetInquiryAsync(id);

    public Task<Inquiry> AddInquiryAsync(Inquiry inquiry) => Dao.AddAsync(inquiry);

    public Task UpdateInquiryAsync(Inquiry inquiry) => Dao.UpdateAsync(inquiry);

    public Task<List<TourBooking>> ListToursAsync(DateOnly from, DateOnly to) => Dao.ListToursAsync(from, to);

    public Task<TourBooking?> GetTourDetailAsync(int id) => Dao.GetTourDetailAsync(id);

    public Task<TourBooking?> GetTourAsync(int id) => Dao.GetTourAsync(id);

    public Task UpdateTourAsync(TourBooking tour) => Dao.UpdateTourAsync(tour);

    public Task<List<TourSlotUsage>> CountGuestsAsync(DateOnly from, DateOnly to) => Dao.CountGuestsAsync(from, to);

    public Task<TourBooking> BookTourAsync(TourBooking tour, int capacity) => Dao.BookTourAsync(tour, capacity);

    public Task CompleteTourAsync(int tourId, DateTime now) => Dao.CompleteTourAsync(tourId, now);
}
