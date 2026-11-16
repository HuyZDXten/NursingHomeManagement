using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;

namespace NursingHome.Repositories.Interfaces;

/// <summary>S3-H6: nhật ký gia đình &amp; phản hồi.</summary>
public interface IFamilyCareRepository
{
    Task<List<TimelineEntry>> TimelineForDateAsync(
        int residentId, DateOnly date, DateTime startUtc, DateTime endUtc, bool includeCare, bool includeHealth);

    Task<List<Feedback>> ListFeedbacksAsync(int? familyAccountId, FeedbackStatus? status, FeedbackCategory? category);
    Task<Feedback?> GetFeedbackAsync(int id);
    Task<Feedback> AddFeedbackAsync(Feedback feedback);
    Task UpdateFeedbackAsync(Feedback feedback);
}
