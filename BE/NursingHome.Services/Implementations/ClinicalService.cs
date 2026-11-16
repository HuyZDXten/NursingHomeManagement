using System.Text.Json;
using Microsoft.Extensions.Options;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Care;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.Services.Implementations;

/// <summary>S3-H5 (UC-64, UC-67, UC-68): đánh giá định kỳ, phiếu khám bác sĩ, ngưỡng sinh hiệu riêng.</summary>
public class ClinicalService(
    IClinicalRepository clinical,
    IResidentRepository residents,
    IResidentAccessService access,
    INotificationService notifications,
    IOptions<FacilityOptions> facility,
    FacilityClock clock) : IClinicalService
{
    public IReadOnlyList<AssessmentScaleDto> GetScales() => AssessmentScales.GetScales();

    public async Task<List<AssessmentDto>> ListAssessmentsAsync(CurrentUser user, int residentId)
    {
        await access.RequireAsync(user, residentId, ResidentSection.Health);
        return (await clinical.ListAssessmentsAsync(residentId)).Select(a => a.ToDto()).ToList();
    }

    public async Task<AssessmentDto> SubmitAssessmentAsync(CurrentUser user, SubmitAssessmentRequest request)
    {
        if (await residents.GetStatusAsync(request.ResidentId) != ResidentStatus.Active)
        {
            throw new BusinessRuleException("Chỉ đánh giá cho cư dân đang lưu trú.");
        }

        if (!Enum.TryParse<AssessmentType>(request.Type, ignoreCase: true, out var type))
        {
            throw new BusinessRuleException("Loại đánh giá không hợp lệ (FallRisk, Barthel, Nutrition).");
        }

        var (score, maxScore, risk) = AssessmentScales.Score(type, request.Answers);
        var saved = await clinical.AddAssessmentAsync(new Assessment
        {
            ResidentId = request.ResidentId,
            Type = type,
            Score = score,
            MaxScore = maxScore,
            RiskLevel = risk,
            AnswersJson = JsonSerializer.Serialize(request.Answers),
            Note = request.Note?.Trim(),
            AssessedById = user.AccountId,
            AssessedAt = clock.UtcNow,
            NextDueOn = clock.Today.AddDays(90)
        });

        if (type == AssessmentType.FallRisk && risk == RiskLevel.High)
        {
            var resident = await residents.GetByIdAsync(request.ResidentId);
            await notifications.NotifyRolesAsync(
                [Roles.Nurse],
                NotificationType.Health,
                $"Nguy cơ té ngã cao: {resident?.FullName}",
                $"Điểm Morse {score}/{maxScore}. Gợi ý: tạo kế hoạch chăm sóc phòng ngã.",
                $"/staff/residents/{request.ResidentId}");
        }

        return saved.ToDto();
    }

    public async Task<List<DueAssessmentDto>> DueAssessmentsAsync()
    {
        var due = await clinical.DueAssessmentsAsync(clock.Today);
        return due.Select(item => new DueAssessmentDto
        {
            ResidentId = item.Resident.Id,
            ResidentCode = item.Resident.Code,
            ResidentName = item.Resident.FullName,
            Type = item.Type.ToString(),
            LastAssessedAt = item.Latest?.AssessedAt,
            LastRiskLevel = item.Latest?.RiskLevel.ToString(),
            NextDueOn = item.Latest?.NextDueOn
        }).ToList();
    }

    public async Task<List<DoctorVisitDto>> ListDoctorVisitsAsync(CurrentUser user, int residentId)
    {
        await access.RequireAsync(user, residentId, ResidentSection.Health);
        return (await clinical.ListDoctorVisitsAsync(residentId)).Select(v => v.ToDto()).ToList();
    }

    public async Task<DoctorVisitDto> CreateDoctorVisitAsync(CurrentUser user, int residentId, SaveDoctorVisitRequest request)
    {
        if (await residents.GetStatusAsync(residentId) != ResidentStatus.Active)
        {
            throw new BusinessRuleException("Cư dân phải đang lưu trú.");
        }

        if (request.FollowUpOn.HasValue && request.FollowUpOn.Value < clock.Today)
        {
            throw new BusinessRuleException("Ngày tái khám không được ở quá khứ.");
        }

        var visitedAt = request.VisitedAt.HasValue
            ? (request.VisitedAt.Value.Kind == DateTimeKind.Utc ? request.VisitedAt.Value : request.VisitedAt.Value.ToUniversalTime())
            : clock.UtcNow;

        var saved = await clinical.AddDoctorVisitAsync(new DoctorVisit
        {
            ResidentId = residentId,
            DoctorId = user.AccountId,
            VisitedAt = visitedAt,
            Subjective = request.Subjective?.Trim(),
            Objective = request.Objective?.Trim(),
            Assessment = request.Assessment.Trim(),
            Plan = request.Plan?.Trim(),
            FollowUpOn = request.FollowUpOn,
            ShareWithFamily = request.ShareWithFamily,
            CreatedAt = clock.UtcNow
        });

        return saved.ToDto();
    }

    public async Task<DoctorVisitDto> UpdateDoctorVisitAsync(CurrentUser user, int residentId, int visitId, SaveDoctorVisitRequest request)
    {
        var visit = await clinical.GetDoctorVisitAsync(visitId) ?? throw new NotFoundException("Không tìm thấy phiếu khám.");
        if (visit.ResidentId != residentId)
        {
            throw new NotFoundException("Không tìm thấy phiếu khám của cư dân này.");
        }

        if (visit.DoctorId != user.AccountId)
        {
            throw new ForbiddenException("Chỉ bác sĩ viết phiếu khám mới được chỉnh sửa.");
        }

        if (clock.UtcNow - visit.CreatedAt > TimeSpan.FromHours(24))
        {
            throw new BusinessRuleException("Chỉ được chỉnh sửa phiếu khám trong vòng 24 giờ sau khi tạo.");
        }

        if (request.FollowUpOn.HasValue && request.FollowUpOn.Value < clock.Today)
        {
            throw new BusinessRuleException("Ngày tái khám không được ở quá khứ.");
        }

        visit.Subjective = request.Subjective?.Trim();
        visit.Objective = request.Objective?.Trim();
        visit.Assessment = request.Assessment.Trim();
        visit.Plan = request.Plan?.Trim();
        visit.FollowUpOn = request.FollowUpOn;
        visit.ShareWithFamily = request.ShareWithFamily;
        visit.UpdatedAt = clock.UtcNow;

        return (await clinical.UpdateDoctorVisitAsync(visit)).ToDto();
    }

    public async Task<ThresholdDto> GetThresholdAsync(CurrentUser user, int residentId)
    {
        await access.RequireAsync(user, residentId, ResidentSection.Health);
        var custom = await clinical.GetThresholdAsync(residentId);
        if (custom is not null)
        {
            return custom.ToDto();
        }

        var d = facility.Value.VitalAlerts;
        return new ThresholdDto
        {
            ResidentId = residentId,
            IsCustom = false,
            SystolicHigh = d.SystolicHigh,
            SystolicLow = d.SystolicLow,
            HeartRateHigh = d.HeartRateHigh,
            HeartRateLow = d.HeartRateLow,
            TemperatureHigh = d.TemperatureHigh,
            SpO2Low = d.SpO2Low
        };
    }

    public async Task<ThresholdDto> SetThresholdAsync(CurrentUser user, int residentId, SaveThresholdRequest request)
    {
        if (await residents.GetStatusAsync(residentId) is null)
        {
            throw new NotFoundException("Không tìm thấy cư dân.");
        }

        if (request.SystolicHigh <= request.SystolicLow)
        {
            throw new BusinessRuleException("Huyết áp tâm thu ngưỡng trên phải lớn hơn ngưỡng dưới.");
        }

        if (request.HeartRateHigh <= request.HeartRateLow)
        {
            throw new BusinessRuleException("Nhịp tim ngưỡng trên phải lớn hơn ngưỡng dưới.");
        }

        var saved = await clinical.UpsertThresholdAsync(new VitalThreshold
        {
            ResidentId = residentId,
            SystolicHigh = request.SystolicHigh,
            SystolicLow = request.SystolicLow,
            HeartRateHigh = request.HeartRateHigh,
            HeartRateLow = request.HeartRateLow,
            TemperatureHigh = request.TemperatureHigh,
            SpO2Low = request.SpO2Low,
            Note = request.Note?.Trim(),
            SetById = user.AccountId,
            UpdatedAt = clock.UtcNow
        });

        return saved.ToDto();
    }
}
