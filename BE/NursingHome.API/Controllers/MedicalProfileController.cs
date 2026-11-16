using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.BusinessObjects.Enums;
using NursingHome.DTOs.Care;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>S3-H2 (UC-66, UC-71, UC-82): bệnh nền, dị ứng, chế độ ăn và cảnh báo khi nhập thuốc.</summary>
[ApiController]
public class MedicalProfileController(IMedicalProfileService medicalProfile) : ControllerBase
{
    private const string MedicalEditors = Roles.Admin + "," + Roles.Doctor + "," + Roles.Nurse;

    [HttpGet("api/residents/{residentId:int}/medical-profile")]
    [Authorize(Policy = AuthPolicies.ClinicalStaff)]
    public async Task<ActionResult<MedicalProfileDto>> Get(int residentId) =>
        Ok(await medicalProfile.GetAsync(User.RequireCurrentUser(), residentId));

    [HttpGet("api/family/residents/{residentId:int}/medical-profile")]
    [Authorize(Policy = AuthPolicies.FamilyOnly)]
    public async Task<ActionResult<MedicalProfileDto>> GetForFamily(int residentId) =>
        Ok(await medicalProfile.GetAsync(User.RequireCurrentUser(), residentId));

    [HttpPost("api/residents/{residentId:int}/conditions")]
    [Authorize(Roles = MedicalEditors)]
    public async Task<ActionResult<ConditionDto>> AddCondition(int residentId, [FromBody] SaveConditionRequest request) =>
        Ok(await medicalProfile.AddConditionAsync(User.RequireCurrentUser(), residentId, request));

    [HttpPut("api/residents/{residentId:int}/conditions/{conditionId:int}")]
    [Authorize(Roles = MedicalEditors)]
    public async Task<ActionResult<ConditionDto>> UpdateCondition(
        int residentId, int conditionId, [FromBody] SaveConditionRequest request) =>
        Ok(await medicalProfile.UpdateConditionAsync(User.RequireCurrentUser(), residentId, conditionId, request));

    [HttpDelete("api/residents/{residentId:int}/conditions/{conditionId:int}")]
    [Authorize(Roles = MedicalEditors)]
    public async Task<IActionResult> DeleteCondition(int residentId, int conditionId)
    {
        await medicalProfile.DeleteConditionAsync(User.RequireCurrentUser(), residentId, conditionId);
        return NoContent();
    }

    [HttpPost("api/residents/{residentId:int}/allergies")]
    [Authorize(Roles = MedicalEditors)]
    public async Task<ActionResult<AllergyDto>> AddAllergy(int residentId, [FromBody] SaveAllergyRequest request) =>
        Ok(await medicalProfile.AddAllergyAsync(User.RequireCurrentUser(), residentId, request));

    [HttpDelete("api/residents/{residentId:int}/allergies/{allergyId:int}")]
    [Authorize(Roles = MedicalEditors)]
    public async Task<IActionResult> DeleteAllergy(int residentId, int allergyId)
    {
        await medicalProfile.DeleteAllergyAsync(User.RequireCurrentUser(), residentId, allergyId);
        return NoContent();
    }

    [HttpPost("api/residents/{residentId:int}/diet-plans")]
    [Authorize(Roles = MedicalEditors)]
    public async Task<ActionResult<DietPlanDto>> SetDietPlan(int residentId, [FromBody] SaveDietPlanRequest request) =>
        Ok(await medicalProfile.SetDietPlanAsync(User.RequireCurrentUser(), residentId, request));

    /// <summary>Kiểm tra tên thuốc với dị ứng thuốc và lịch thuốc đang dùng trước khi kê/lưu.</summary>
    [HttpPost("api/medication-schedules/check")]
    [Authorize(Policy = AuthPolicies.Prescribers)]
    public async Task<ActionResult<List<MedicationWarningDto>>> CheckMedication([FromBody] CheckMedicationRequest request) =>
        Ok(await medicalProfile.CheckMedicationAsync(request.ResidentId, request.MedicationName));
}
