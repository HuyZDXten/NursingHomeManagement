using NursingHome.DTOs.Care;
using NursingHome.DTOs.Common;

namespace NursingHome.Services.Interfaces;

// MODULE CHĂM SÓC & GIA ĐÌNH (Huy): khai báo interface service của module ở file này
// (không thêm vào IServices.cs của phần lõi).

/// <summary>S3-H1 (UC-58, UC-59, UC-107): báo cáo và xử lý sự cố.</summary>
public interface IIncidentService
{
    Task<List<IncidentDto>> ListAsync(CurrentUser user, int? residentId, string? status, string? severity, DateOnly? from, DateOnly? to);

    Task<IncidentDto> GetAsync(CurrentUser user, int id);

    Task<IncidentDto> ReportAsync(CurrentUser user, ReportIncidentRequest request);

    Task<IncidentDto> UpdateAsync(CurrentUser user, int id, UpdateIncidentRequest request);

    Task<List<FamilyIncidentDto>> ListForFamilyAsync(CurrentUser user, int residentId);
}

/// <summary>S3-H2 (UC-66, UC-71, UC-82): bệnh nền, dị ứng, chế độ ăn và kiểm tra cảnh báo thuốc.</summary>
public interface IMedicalProfileService
{
    Task<MedicalProfileDto> GetAsync(CurrentUser user, int residentId);

    Task<ConditionDto> AddConditionAsync(CurrentUser user, int residentId, SaveConditionRequest request);

    Task<ConditionDto> UpdateConditionAsync(CurrentUser user, int residentId, int conditionId, SaveConditionRequest request);

    Task DeleteConditionAsync(CurrentUser user, int residentId, int conditionId);

    Task<AllergyDto> AddAllergyAsync(CurrentUser user, int residentId, SaveAllergyRequest request);

    Task DeleteAllergyAsync(CurrentUser user, int residentId, int allergyId);

    Task<DietPlanDto> SetDietPlanAsync(CurrentUser user, int residentId, SaveDietPlanRequest request);

    Task<List<MedicationWarningDto>> CheckMedicationAsync(int residentId, string medicationName);
}

/// <summary>S3-H3 (UC-103, UC-104): đặt &amp; duyệt lịch thăm.</summary>
public interface IVisitService
{
    Task<List<VisitSlotDto>> GetSlotsAsync(DateOnly date);
    Task<VisitBookingDto> BookAsync(CurrentUser user, BookVisitRequest request);
    Task<List<VisitBookingDto>> ListMineAsync(CurrentUser user);
    Task<VisitBookingDto> CancelAsync(CurrentUser user, int id);
    Task<List<VisitBookingDto>> ListByDateAsync(DateOnly date, string? status);
    Task<VisitBookingDto> ApproveAsync(CurrentUser user, int id, ReviewVisitRequest? request);
    Task<VisitBookingDto> RejectAsync(CurrentUser user, int id, ReviewVisitRequest request);
    Task<VisitBookingDto> CheckInAsync(CurrentUser user, int id);
    Task<VisitBookingDto> CompleteAsync(CurrentUser user, int id);
    Task<VisitBookingDto> NoShowAsync(CurrentUser user, int id);
}

/// <summary>S3-H4 (UC-51): kế hoạch chăm sóc và sinh việc tự động.</summary>
public interface ICarePlanService
{
    Task<List<CarePlanDto>> ListByResidentAsync(CurrentUser user, int residentId);
    Task<CarePlanDto> CreateAsync(CurrentUser user, SaveCarePlanRequest request);
    Task<CarePlanDto> UpdateAsync(CurrentUser user, int id, SaveCarePlanRequest request);
    Task DeactivateAsync(int id);
    Task<GenerateTasksResultDto> GenerateForDatesAsync(int days = 2, int? onlyResidentId = null);
}

/// <summary>S3-H4 (UC-56, UC-57): ghi nhận ADL &amp; bữa ăn hằng ngày.</summary>
public interface IDailyLogService
{
    Task<List<DailyCareLogDto>> ListLogsAsync(CurrentUser user, int residentId, DateOnly date);
    Task<DailyCareLogDto> AddLogAsync(CurrentUser user, int residentId, CreateDailyCareLogRequest request);
    Task<List<MealLogDto>> ListMealsAsync(CurrentUser user, int residentId, DateOnly date);
    Task<MealLogDto> UpsertMealAsync(CurrentUser user, int residentId, UpsertMealLogRequest request);
}

/// <summary>S3-H4 (UC-61): nhắc việc sắp đến hạn / quá hạn.</summary>
public interface ITaskReminderService
{
    Task RunOnceAsync();
}

/// <summary>S3-H5 (UC-64, UC-67, UC-68): đánh giá định kỳ, phiếu khám bác sĩ, ngưỡng sinh hiệu riêng.</summary>
public interface IClinicalService
{
    IReadOnlyList<AssessmentScaleDto> GetScales();
    Task<List<AssessmentDto>> ListAssessmentsAsync(CurrentUser user, int residentId);
    Task<AssessmentDto> SubmitAssessmentAsync(CurrentUser user, SubmitAssessmentRequest request);
    Task<List<DueAssessmentDto>> DueAssessmentsAsync();

    Task<List<DoctorVisitDto>> ListDoctorVisitsAsync(CurrentUser user, int residentId);
    Task<DoctorVisitDto> CreateDoctorVisitAsync(CurrentUser user, int residentId, SaveDoctorVisitRequest request);
    Task<DoctorVisitDto> UpdateDoctorVisitAsync(CurrentUser user, int residentId, int visitId, SaveDoctorVisitRequest request);

    Task<ThresholdDto> GetThresholdAsync(CurrentUser user, int residentId);
    Task<ThresholdDto> SetThresholdAsync(CurrentUser user, int residentId, SaveThresholdRequest request);
}

/// <summary>S3-H6 (UC-108): dòng thời gian hằng ngày cho gia đình.</summary>
public interface IFamilyTimelineService
{
    Task<List<TimelineItemDto>> GetForDateAsync(CurrentUser user, int residentId, DateOnly date);
}

/// <summary>S3-H6 (UC-109, UC-110): phản hồi / khiếu nại gia đình.</summary>
public interface IFeedbackService
{
    Task<List<FeedbackDto>> ListMineAsync(CurrentUser user);
    Task<FeedbackDto> CreateAsync(CurrentUser user, CreateFeedbackRequest request);
    Task<List<FeedbackDto>> ListAllAsync(string? status, string? category);
    Task<FeedbackDto> RespondAsync(CurrentUser user, int id, RespondFeedbackRequest request);
}

/// <summary>S4-H2 (AI-03, AI-06): nhật ký hằng ngày AI &amp; diễn giải xu hướng sinh hiệu.</summary>
public interface IDailyDigestService
{
    Task<int> GenerateForDateAsync(DateOnly forDate, CancellationToken cancellationToken = default);
    Task<List<AiInsightDto>> ListAsync(DateOnly? date, string? type, string? status);
    Task<AiInsightDto> ApproveAsync(CurrentUser user, long id, ApproveInsightRequest request);
    Task<AiInsightDto> RejectAsync(CurrentUser user, long id);
    Task<List<AiInsightDto>> ListForFamilyAsync(CurrentUser user, int residentId, DateOnly? date);
}

public interface IVitalTrendService
{
    Task<int> AnalyzeForDateAsync(DateOnly forDate, CancellationToken cancellationToken = default);
    Task<List<AiInsightDto>> ListForResidentAsync(CurrentUser user, int residentId);
}
