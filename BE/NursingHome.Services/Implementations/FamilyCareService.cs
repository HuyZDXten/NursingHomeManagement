using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Care;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>S3-H6 (UC-108): nhật ký hằng ngày gộp từ các nguồn đã chia sẻ cho gia đình.</summary>
public class FamilyTimelineService(
    IFamilyCareRepository familyCare,
    IResidentRepository residents,
    FacilityClock clock) : IFamilyTimelineService
{
    public async Task<List<TimelineItemDto>> GetForDateAsync(CurrentUser user, int residentId, DateOnly date)
    {
        var link = await residents.GetLinkAsync(residentId, user.AccountId)
            ?? throw new NotFoundException("Không tìm thấy cư dân.");

        var today = clock.Today;
        if (date > today || date < today.AddDays(-30))
        {
            throw new BusinessRuleException("Chỉ xem được nhật ký trong vòng 30 ngày gần đây.");
        }

        if (!link.CanViewCare && !link.CanViewHealth)
        {
            return [];
        }

        var startUtc = clock.StartOfDayUtc(date);
        var entries = await familyCare.TimelineForDateAsync(
            residentId, date, startUtc, startUtc.AddDays(1), link.CanViewCare, link.CanViewHealth);

        return entries.Select(e => new TimelineItemDto
        {
            Kind = e.Kind,
            At = e.At,
            Title = e.Title,
            Detail = e.Detail,
            RecordedBy = e.RecordedBy
        }).ToList();
    }
}

/// <summary>S3-H6 (UC-109, UC-110): phản hồi / khiếu nại gia đình.</summary>
public class FeedbackService(
    IFamilyCareRepository familyCare,
    IResidentRepository residents,
    IAccountRepository accounts,
    INotificationService notifications,
    FacilityClock clock) : IFeedbackService
{
    public async Task<List<FeedbackDto>> ListMineAsync(CurrentUser user) =>
        (await familyCare.ListFeedbacksAsync(user.AccountId, null, null)).Select(f => f.ToDto()).ToList();

    public async Task<FeedbackDto> CreateAsync(CurrentUser user, CreateFeedbackRequest request)
    {
        if (request.ResidentId.HasValue &&
            await residents.GetLinkAsync(request.ResidentId.Value, user.AccountId) is null)
        {
            throw new NotFoundException("Không tìm thấy cư dân liên kết.");
        }

        if (!Enum.TryParse<FeedbackCategory>(request.Category, ignoreCase: true, out var category))
        {
            throw new BusinessRuleException("Nhóm phản hồi không hợp lệ (Care, Food, Facility, Billing, Staff, Other).");
        }

        var saved = await familyCare.AddFeedbackAsync(new Feedback
        {
            FamilyAccountId = user.AccountId,
            ResidentId = request.ResidentId,
            Category = category,
            Rating = request.Rating,
            Subject = request.Subject.Trim(),
            Content = request.Content.Trim(),
            Status = FeedbackStatus.New,
            CreatedAt = clock.UtcNow
        });

        await notifications.NotifyRolesAsync(
            [Roles.Admin],
            NotificationType.System,
            $"Phản hồi mới từ gia đình: {saved.Subject}",
            $"{user.Name} ({category})",
            "/staff/feedback");

        return (await familyCare.GetFeedbackAsync(saved.Id))!.ToDto();
    }

    public async Task<List<FeedbackDto>> ListAllAsync(string? status, string? category)
    {
        FeedbackStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<FeedbackStatus>(status, ignoreCase: true, out var s))
            {
                throw new BusinessRuleException("Trạng thái phản hồi không hợp lệ.");
            }
            parsedStatus = s;
        }

        FeedbackCategory? parsedCategory = null;
        if (!string.IsNullOrWhiteSpace(category))
        {
            if (!Enum.TryParse<FeedbackCategory>(category, ignoreCase: true, out var c))
            {
                throw new BusinessRuleException("Nhóm phản hồi không hợp lệ.");
            }
            parsedCategory = c;
        }

        return (await familyCare.ListFeedbacksAsync(null, parsedStatus, parsedCategory)).Select(f => f.ToDto()).ToList();
    }

    public async Task<FeedbackDto> RespondAsync(CurrentUser user, int id, RespondFeedbackRequest request)
    {
        var feedback = await familyCare.GetFeedbackAsync(id) ?? throw new NotFoundException("Không tìm thấy phản hồi.");

        if (!Enum.TryParse<FeedbackStatus>(request.Status, ignoreCase: true, out var status) ||
            status is not (FeedbackStatus.InProgress or FeedbackStatus.Resolved))
        {
            throw new BusinessRuleException("Trạng thái xử lý chỉ nhận InProgress hoặc Resolved.");
        }

        string? response = request.Response?.Trim();
        if (status == FeedbackStatus.Resolved && string.IsNullOrWhiteSpace(response))
        {
            throw new BusinessRuleException("Khi đánh dấu đã xử lý bắt buộc nhập nội dung trả lời.");
        }

        if (request.AssignedToId.HasValue &&
            !await accounts.IsActiveInRolesAsync(request.AssignedToId.Value, Roles.Staff))
        {
            throw new BusinessRuleException("Người được giao xử lý phải là nhân viên đang hoạt động.");
        }

        feedback.Status = status;
        feedback.AssignedToId = request.AssignedToId ?? feedback.AssignedToId;
        if (!string.IsNullOrWhiteSpace(response))
        {
            feedback.Response = response;
            feedback.RespondedById = user.AccountId;
            feedback.RespondedAt = clock.UtcNow;
        }

        await familyCare.UpdateFeedbackAsync(feedback);

        await notifications.NotifyAsync(
            [feedback.FamilyAccountId],
            NotificationType.System,
            $"Cơ sở đã phản hồi: {feedback.Subject}",
            response ?? "Phản hồi của gia đình đang được tiếp nhận xử lý.",
            "/family/feedback");

        return (await familyCare.GetFeedbackAsync(id))!.ToDto();
    }
}
