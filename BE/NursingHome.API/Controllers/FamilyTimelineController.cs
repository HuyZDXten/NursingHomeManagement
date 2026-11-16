using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Care;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>S3-H6 (UC-108): nhật ký hằng ngày tổng hợp cho gia đình.</summary>
[ApiController]
[Route("api/family/residents/{residentId:int}/timeline")]
[Authorize(Policy = AuthPolicies.FamilyOnly)]
public class FamilyTimelineController(IFamilyTimelineService timeline, FacilityClock clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TimelineItemDto>>> Get(int residentId, [FromQuery] DateOnly? date) =>
        Ok(await timeline.GetForDateAsync(User.RequireCurrentUser(), residentId, date ?? clock.Today));
}

/// <summary>S3-H6 (UC-109, UC-110): phản hồi / khiếu nại từ gia đình.</summary>
[ApiController]
[Route("api/feedbacks")]
public class FeedbacksController(IFeedbackService feedbacks) : ControllerBase
{
    [HttpGet("mine")]
    [Authorize(Policy = AuthPolicies.FamilyOnly)]
    public async Task<ActionResult<List<FeedbackDto>>> Mine() =>
        Ok(await feedbacks.ListMineAsync(User.RequireCurrentUser()));

    [HttpPost]
    [Authorize(Policy = AuthPolicies.FamilyOnly)]
    public async Task<ActionResult<FeedbackDto>> Create([FromBody] CreateFeedbackRequest request) =>
        Ok(await feedbacks.CreateAsync(User.RequireCurrentUser(), request));

    [HttpGet]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<List<FeedbackDto>>> ListAll([FromQuery] string? status, [FromQuery] string? category) =>
        Ok(await feedbacks.ListAllAsync(status, category));

    [HttpPost("{id:int}/respond")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<FeedbackDto>> Respond(int id, [FromBody] RespondFeedbackRequest request) =>
        Ok(await feedbacks.RespondAsync(User.RequireCurrentUser(), id, request));
}
