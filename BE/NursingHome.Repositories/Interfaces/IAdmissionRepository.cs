using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;

namespace NursingHome.Repositories.Interfaces;

/// <summary>MODULE VẬN HÀNH (Viên) — S3-V1 tiếp nhận &amp; tham quan.</summary>
public interface IAdmissionRepository
{
    Task<List<Inquiry>> ListInquiriesAsync();

    Task<Inquiry?> GetInquiryDetailAsync(int id);

    Task<Inquiry?> GetInquiryAsync(int id);

    Task<Inquiry> AddInquiryAsync(Inquiry inquiry);

    Task UpdateInquiryAsync(Inquiry inquiry);

    Task<List<TourBooking>> ListToursAsync(DateOnly from, DateOnly to);

    Task<TourBooking?> GetTourDetailAsync(int id);

    Task<TourBooking?> GetTourAsync(int id);

    Task UpdateTourAsync(TourBooking tour);

    Task<List<TourSlotUsage>> CountGuestsAsync(DateOnly from, DateOnly to);

    Task<TourBooking> BookTourAsync(TourBooking tour, int capacity);

    Task CompleteTourAsync(int tourId, DateTime now);
}
