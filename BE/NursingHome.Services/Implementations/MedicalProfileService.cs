using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Care;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>S3-H2 (UC-66, UC-71, UC-82): hồ sơ bệnh nền, dị ứng, chế độ ăn và kiểm tra cảnh báo thuốc.</summary>
public class MedicalProfileService(
    IMedicalProfileRepository profile,
    IMedicationRepository medications,
    IResidentRepository residents,
    IResidentAccessService access,
    FacilityClock clock) : IMedicalProfileService
{
    public async Task<MedicalProfileDto> GetAsync(CurrentUser user, int residentId)
    {
        await access.RequireAsync(user, residentId, ResidentSection.Health);
        var conditions = await profile.ListConditionsAsync(residentId);
        var allergies = await profile.ListAllergiesAsync(residentId);
        var diets = await profile.ListDietPlansAsync(residentId);

        var dietDtos = diets.Select(d => d.ToDto()).ToList();
        return new MedicalProfileDto
        {
            Conditions = conditions.Select(c => c.ToDto()).ToList(),
            Allergies = allergies.Select(a => a.ToDto()).ToList(),
            ActiveDiet = dietDtos.FirstOrDefault(d => d.EndDate is null),
            DietHistory = dietDtos
        };
    }

    public async Task<ConditionDto> AddConditionAsync(CurrentUser user, int residentId, SaveConditionRequest request)
    {
        await RequireEditorAsync(user, residentId);
        var status = ParseEnum<ConditionStatus>(request.Status, "Trạng thái bệnh nền không hợp lệ (Active hoặc Resolved).");
        var saved = await profile.AddConditionAsync(new MedicalCondition
        {
            ResidentId = residentId,
            Name = request.Name.Trim(),
            DiagnosedOn = request.DiagnosedOn,
            Status = status,
            Notes = request.Notes?.Trim(),
            RecordedById = user.AccountId,
            CreatedAt = clock.UtcNow
        });

        var dto = saved.ToDto();
        dto.RecordedByName = user.Name;
        return dto;
    }

    public async Task<ConditionDto> UpdateConditionAsync(CurrentUser user, int residentId, int conditionId, SaveConditionRequest request)
    {
        await RequireEditorAsync(user, residentId);
        var condition = await profile.GetConditionAsync(conditionId)
            ?? throw new NotFoundException("Không tìm thấy bệnh nền.");
        if (condition.ResidentId != residentId)
        {
            throw new NotFoundException("Không tìm thấy bệnh nền của cư dân này.");
        }

        condition.Name = request.Name.Trim();
        condition.DiagnosedOn = request.DiagnosedOn;
        condition.Status = ParseEnum<ConditionStatus>(request.Status, "Trạng thái bệnh nền không hợp lệ (Active hoặc Resolved).");
        condition.Notes = request.Notes?.Trim();
        condition.UpdatedAt = clock.UtcNow;
        await profile.UpdateConditionAsync(condition);
        return condition.ToDto();
    }

    public async Task DeleteConditionAsync(CurrentUser user, int residentId, int conditionId)
    {
        await RequireEditorAsync(user, residentId);
        var condition = await profile.GetConditionAsync(conditionId)
            ?? throw new NotFoundException("Không tìm thấy bệnh nền.");
        if (condition.ResidentId != residentId)
        {
            throw new NotFoundException("Không tìm thấy bệnh nền của cư dân này.");
        }

        await profile.DeleteConditionAsync(condition);
    }

    public async Task<AllergyDto> AddAllergyAsync(CurrentUser user, int residentId, SaveAllergyRequest request)
    {
        await RequireEditorAsync(user, residentId);
        var type = ParseEnum<AllergyType>(request.Type, "Loại dị ứng không hợp lệ (Drug, Food hoặc Other).");
        var severity = ParseEnum<AllergySeverity>(request.Severity, "Mức độ dị ứng không hợp lệ (Mild, Moderate hoặc Severe).");
        var saved = await profile.AddAllergyAsync(new Allergy
        {
            ResidentId = residentId,
            Allergen = request.Allergen.Trim(),
            Type = type,
            Reaction = request.Reaction?.Trim(),
            Severity = severity,
            RecordedById = user.AccountId,
            CreatedAt = clock.UtcNow
        });

        var dto = saved.ToDto();
        dto.RecordedByName = user.Name;
        return dto;
    }

    public async Task DeleteAllergyAsync(CurrentUser user, int residentId, int allergyId)
    {
        await RequireEditorAsync(user, residentId);
        var allergy = await profile.GetAllergyAsync(allergyId)
            ?? throw new NotFoundException("Không tìm thấy thông tin dị ứng.");
        if (allergy.ResidentId != residentId)
        {
            throw new NotFoundException("Không tìm thấy dị ứng của cư dân này.");
        }

        await profile.DeleteAllergyAsync(allergy);
    }

    public async Task<DietPlanDto> SetDietPlanAsync(CurrentUser user, int residentId, SaveDietPlanRequest request)
    {
        await RequireEditorAsync(user, residentId);
        var dietType = ParseEnum<DietType>(request.DietType, "Loại chế độ ăn không hợp lệ.");
        var texture = ParseEnum<DietTexture>(request.Texture, "Độ mềm/kết cấu thức ăn không hợp lệ.");

        var plan = await profile.ReplaceDietPlanAsync(new DietPlan
        {
            ResidentId = residentId,
            DietType = dietType,
            Texture = texture,
            Restrictions = request.Restrictions?.Trim(),
            StartDate = request.StartDate ?? clock.Today,
            EndDate = null,
            PrescribedById = user.AccountId,
            CreatedAt = clock.UtcNow
        });

        return plan.ToDto();
    }

    public async Task<List<MedicationWarningDto>> CheckMedicationAsync(int residentId, string medicationName)
    {
        var warnings = new List<MedicationWarningDto>();
        string normMed = TextMatch.Normalize(medicationName);
        if (string.IsNullOrEmpty(normMed))
        {
            return warnings;
        }

        var drugAllergies = await profile.ListDrugAllergiesAsync(residentId);
        foreach (var allergy in drugAllergies)
        {
            string normAllergen = TextMatch.Normalize(allergy.Allergen);
            if (!string.IsNullOrEmpty(normAllergen) && normMed.Contains(normAllergen, StringComparison.Ordinal))
            {
                string reaction = string.IsNullOrWhiteSpace(allergy.Reaction) ? "chưa ghi phản ứng" : allergy.Reaction;
                warnings.Add(new MedicationWarningDto
                {
                    Kind = "Allergy",
                    Message = $"Cư dân dị ứng {allergy.Allergen} ({allergy.Severity}): {reaction}."
                });
            }
        }

        var activeSchedules = await medications.ListSchedulesAsync(residentId, null);
        foreach (var schedule in activeSchedules.Where(s => s.IsActive))
        {
            if (TextMatch.Normalize(schedule.MedicationName) == normMed)
            {
                warnings.Add(new MedicationWarningDto
                {
                    Kind = "Duplicate",
                    Message = $"Cư dân đang có lịch thuốc cùng tên: {schedule.MedicationName} ({schedule.Dose}, {schedule.ScheduledTime:HH\\:mm})."
                });
            }
        }

        return warnings;
    }

    private async Task RequireEditorAsync(CurrentUser user, int residentId)
    {
        if (!user.IsInRole(Roles.Admin, Roles.Doctor, Roles.Nurse))
        {
            throw new ForbiddenException("Chỉ Bác sĩ, Điều dưỡng hoặc Admin được chỉnh sửa hồ sơ y tế.");
        }

        if (await residents.GetStatusAsync(residentId) is null)
        {
            throw new NotFoundException("Không tìm thấy cư dân.");
        }
    }

    private static TEnum ParseEnum<TEnum>(string value, string errorMessage) where TEnum : struct, Enum
    {
        if (!Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed))
        {
            throw new BusinessRuleException(errorMessage);
        }

        return parsed;
    }
}
