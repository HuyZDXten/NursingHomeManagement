using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class FamilyCareRepository : IFamilyCareRepository
{
    public Task<List<TimelineEntry>> TimelineForDateAsync(
        int residentId, DateOnly date, DateTime startUtc, DateTime endUtc, bool includeCare, bool includeHealth) =>
        FamilyTimelineDAO.Instance.ForDateAsync(residentId, date, startUtc, endUtc, includeCare, includeHealth);

    public Task<List<Feedback>> ListFeedbacksAsync(int? familyAccountId, FeedbackStatus? status, FeedbackCategory? category) =>
        FeedbackDAO.Instance.ListAsync(familyAccountId, status, category);

    public Task<Feedback?> GetFeedbackAsync(int id) => FeedbackDAO.Instance.GetDetailAsync(id);
    public Task<Feedback> AddFeedbackAsync(Feedback feedback) => FeedbackDAO.Instance.AddAsync(feedback);
    public Task UpdateFeedbackAsync(Feedback feedback) => FeedbackDAO.Instance.UpdateAsync(feedback);
}
