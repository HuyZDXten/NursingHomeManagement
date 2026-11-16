using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.BusinessObjects.Enums;
using NursingHome.DTOs.Care;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>S3-H5 (UC-64, UC-67, UC-68): đánh giá định kỳ, phiếu khám bác sĩ và ngưỡng sinh hiệu riêng.</summary>
[ApiController]
public class ClinicalController(IClinicalService clinical) : ControllerBase
{
    private const string PlannersAndDoctor = Roles.Admin + "," + Roles.Nurse + "," + Roles.Doctor;

    [HttpGet("api/assessments/scales")]
    [Authorize(Policy = AuthPolicies.ClinicalStaff)]
    public ActionResult<IReadOnlyList<AssessmentScaleDto>> Scales() => Ok(clinical.GetScales());

    [HttpGet("api/residents/{residentId:int}/assessments")]
    [Authorize(Policy = AuthPolicies.ClinicalStaff)]
    public async Task<ActionResult<List<AssessmentDto>>> ListAssessments(int residentId) =>
        Ok(await clinical.ListAssessmentsAsync(User.RequireCurrentUser(), residentId));

    [HttpPost("api/assessments")]
    [Authorize(Roles = PlannersAndDoctor)]
    public async Task<ActionResult<AssessmentDto>> SubmitAssessment([FromBody] SubmitAssessmentRequest request) =>
        Ok(await clinical.SubmitAssessmentAsync(User.RequireCurrentUser(), request));

    [HttpGet("api/assessments/due")]
    [Authorize(Roles = PlannersAndDoctor)]
    public async Task<ActionResult<List<DueAssessmentDto>>> DueAssessments() =>
        Ok(await clinical.DueAssessmentsAsync());

    [HttpGet("api/residents/{residentId:int}/doctor-visits")]
    [Authorize(Policy = AuthPolicies.ClinicalStaff)]
    public async Task<ActionResult<List<DoctorVisitDto>>> ListDoctorVisits(int residentId) =>
        Ok(await clinical.ListDoctorVisitsAsync(User.RequireCurrentUser(), residentId));

    [HttpPost("api/residents/{residentId:int}/doctor-visits")]
    [Authorize(Roles = Roles.Doctor)]
    public async Task<ActionResult<DoctorVisitDto>> CreateDoctorVisit(
        int residentId, [FromBody] SaveDoctorVisitRequest request) =>
        Ok(await clinical.CreateDoctorVisitAsync(User.RequireCurrentUser(), residentId, request));

    [HttpPut("api/residents/{residentId:int}/doctor-visits/{visitId:int}")]
    [Authorize(Roles = Roles.Doctor)]
    public async Task<ActionResult<DoctorVisitDto>> UpdateDoctorVisit(
        int residentId, int visitId, [FromBody] SaveDoctorVisitRequest request) =>
        Ok(await clinical.UpdateDoctorVisitAsync(User.RequireCurrentUser(), residentId, visitId, request));

    [HttpGet("api/residents/{residentId:int}/vital-thresholds")]
    [Authorize(Policy = AuthPolicies.ClinicalStaff)]
    public async Task<ActionResult<ThresholdDto>> GetThreshold(int residentId) =>
        Ok(await clinical.GetThresholdAsync(User.RequireCurrentUser(), residentId));

    [HttpPut("api/residents/{residentId:int}/vital-thresholds")]
    [Authorize(Policy = AuthPolicies.Prescribers)]
    public async Task<ActionResult<ThresholdDto>> SetThreshold(int residentId, [FromBody] SaveThresholdRequest request) =>
        Ok(await clinical.SetThresholdAsync(User.RequireCurrentUser(), residentId, request));
}
