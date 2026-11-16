using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.BusinessObjects.Enums;
using NursingHome.DTOs.Care;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>S3-H3 (UC-103, UC-104): đặt &amp; duyệt lịch thăm.</summary>
[ApiController]
[Route("api/visits")]
public class VisitsController(IVisitService visits, FacilityClock clock) : ControllerBase
{
    [HttpGet("slots")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Receptionist + "," + Roles.Family)]
    public async Task<ActionResult<List<VisitSlotDto>>> Slots([FromQuery] DateOnly? date) =>
        Ok(await visits.GetSlotsAsync(date ?? clock.Today));

    [HttpPost]
    [Authorize(Policy = AuthPolicies.FamilyOnly)]
    public async Task<ActionResult<VisitBookingDto>> Book([FromBody] BookVisitRequest request) =>
        Ok(await visits.BookAsync(User.RequireCurrentUser(), request));

    [HttpGet("mine")]
    [Authorize(Policy = AuthPolicies.FamilyOnly)]
    public async Task<ActionResult<List<VisitBookingDto>>> Mine() =>
        Ok(await visits.ListMineAsync(User.RequireCurrentUser()));

    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = AuthPolicies.FamilyOnly)]
    public async Task<ActionResult<VisitBookingDto>> Cancel(int id) =>
        Ok(await visits.CancelAsync(User.RequireCurrentUser(), id));

    [HttpGet]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<List<VisitBookingDto>>> List([FromQuery] DateOnly? date, [FromQuery] string? status) =>
        Ok(await visits.ListByDateAsync(date ?? clock.Today, status));

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<VisitBookingDto>> Approve(int id, [FromBody] ReviewVisitRequest? request) =>
        Ok(await visits.ApproveAsync(User.RequireCurrentUser(), id, request));

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<VisitBookingDto>> Reject(int id, [FromBody] ReviewVisitRequest request) =>
        Ok(await visits.RejectAsync(User.RequireCurrentUser(), id, request));

    [HttpPost("{id:int}/check-in")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<VisitBookingDto>> CheckIn(int id) =>
        Ok(await visits.CheckInAsync(User.RequireCurrentUser(), id));

    [HttpPost("{id:int}/complete")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<VisitBookingDto>> Complete(int id) =>
        Ok(await visits.CompleteAsync(User.RequireCurrentUser(), id));

    [HttpPost("{id:int}/no-show")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<VisitBookingDto>> NoShow(int id) =>
        Ok(await visits.NoShowAsync(User.RequireCurrentUser(), id));
}
