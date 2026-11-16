using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.DTOs.Care;
using NursingHome.Services.Ai;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>S4-H2 (AI-03, AI-06): duyệt nhật ký hằng ngày AI và xem diễn giải xu hướng sinh hiệu.</summary>
[ApiController]
public class AiInsightsController(
    IDailyDigestService digests,
    IVitalTrendService trends,
    FacilityClock clock) : ControllerBase
{
    private const string PlannersAndDoctor = Roles.Admin + "," + Roles.Nurse + "," + Roles.Doctor;

    [HttpGet("api/ai-insights")]
    [Authorize(Roles = PlannersAndDoctor)]
    public async Task<ActionResult<List<AiInsightDto>>> List(
        [FromQuery] DateOnly? date,
        [FromQuery] string? type,
        [FromQuery] string? status) =>
        Ok(await digests.ListAsync(date, type, status));

    [HttpPost("api/ai-insights/{id:long}/approve")]
    [Authorize(Policy = AuthPolicies.CarePlanners)]
    public async Task<ActionResult<AiInsightDto>> Approve(long id, [FromBody] ApproveInsightRequest request) =>
        Ok(await digests.ApproveAsync(User.RequireCurrentUser(), id, request));

    [HttpPost("api/ai-insights/{id:long}/reject")]
    [Authorize(Policy = AuthPolicies.CarePlanners)]
    public async Task<ActionResult<AiInsightDto>> Reject(long id) =>
        Ok(await digests.RejectAsync(User.RequireCurrentUser(), id));

    /// <summary>Chạy tay để demo/kiểm thử không cần chờ job 06:00 / 19:00.</summary>
    [HttpPost("api/ai-insights/generate")]
    [Authorize(Policy = AuthPolicies.CarePlanners)]
    public async Task<ActionResult<GenerateInsightsResultDto>> Generate(
        [FromQuery] string type = nameof(InsightType.DailyDigest),
        [FromQuery] DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        var targetDate = date ?? clock.Today;
        int created = type.Equals(nameof(InsightType.VitalTrend), StringComparison.OrdinalIgnoreCase)
            ? await trends.AnalyzeForDateAsync(targetDate, cancellationToken)
            : await digests.GenerateForDateAsync(targetDate, cancellationToken);
        return Ok(new GenerateInsightsResultDto { CreatedCount = created });
    }

    [HttpGet("api/residents/{residentId:int}/vital-trends")]
    [Authorize(Policy = AuthPolicies.ClinicalStaff)]
    public async Task<ActionResult<List<AiInsightDto>>> VitalTrends(int residentId) =>
        Ok(await trends.ListForResidentAsync(User.RequireCurrentUser(), residentId));

    [HttpGet("api/family/residents/{residentId:int}/digests")]
    [Authorize(Policy = AuthPolicies.FamilyOnly)]
    public async Task<ActionResult<List<AiInsightDto>>> FamilyDigests(int residentId, [FromQuery] DateOnly? date) =>
        Ok(await digests.ListForFamilyAsync(User.RequireCurrentUser(), residentId, date));
}
