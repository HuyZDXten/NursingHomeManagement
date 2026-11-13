using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Staffing;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>
/// MODULE VẬN HÀNH (Viên) — S3-V4: UC-47 nghỉ phép/đổi ca, UC-49 bàn giao ca.
/// Lễ tân, kế toán, gia đình không có ca → 403 (policy ClinicalStaff).
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.ClinicalStaff)]
public class HandoversController(IHandoverService handovers) : ControllerBase
{
    // ---------------- Nghỉ phép / đổi ca ----------------

    /// <param name="status">Pending | Approved | Rejected | Cancelled (bỏ trống = tất cả).</param>
    [HttpGet("api/leave-requests")]
    public async Task<ActionResult<List<LeaveRequestDto>>> LeaveRequests([FromQuery] string? status) =>
        Ok(await handovers.ListLeaveRequestsAsync(User.RequireCurrentUser(), status));

    [HttpPost("api/leave-requests")]
    public async Task<ActionResult<LeaveRequestDto>> CreateLeaveRequest([FromBody] CreateLeaveRequest request) =>
        Ok(await handovers.CreateLeaveRequestAsync(User.RequireCurrentUser(), request));

    /// <summary>Ca của đồng nghiệp cùng vai trò có thể đổi lấy ca <paramref name="shiftAssignmentId"/> của mình.</summary>
    [HttpGet("api/leave-requests/swap-candidates")]
    public async Task<ActionResult<List<ShiftAssignmentDto>>> SwapCandidates([FromQuery] int shiftAssignmentId) =>
        Ok(await handovers.SwapCandidatesAsync(User.RequireCurrentUser(), shiftAssignmentId));

    [HttpPost("api/leave-requests/{id:int}/approve")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<LeaveApprovalDto>> Approve(int id, [FromBody] ReviewLeaveRequest? request) =>
        Ok(await handovers.ApproveAsync(User.RequireCurrentUser(), id, request?.Note));

    [HttpPost("api/leave-requests/{id:int}/reject")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<LeaveRequestDto>> Reject(int id, [FromBody] ReviewLeaveRequest? request) =>
        Ok(await handovers.RejectAsync(User.RequireCurrentUser(), id, request?.Note));

    [HttpPost("api/leave-requests/{id:int}/cancel")]
    public async Task<ActionResult<LeaveRequestDto>> Cancel(int id) =>
        Ok(await handovers.CancelAsync(User.RequireCurrentUser(), id));

    // ---------------- Bàn giao ca ----------------

    /// <param name="date">Ngày bắt đầu ca (yyyy-MM-dd), mặc định hôm nay.</param>
    [HttpGet("api/handovers/draft")]
    public async Task<ActionResult<HandoverDraftDto>> Draft([FromQuery] int shiftId, [FromQuery] DateOnly? date) =>
        Ok(await handovers.GetDraftAsync(User.RequireCurrentUser(), shiftId, date));

    [HttpGet("api/handovers")]
    public async Task<ActionResult<List<HandoverDto>>> List([FromQuery] DateOnly? date) =>
        Ok(await handovers.ListHandoversAsync(User.RequireCurrentUser(), date));

    /// <summary>Bàn giao chưa đọc mà người gọi cần xác nhận (2 ngày gần nhất).</summary>
    [HttpGet("api/handovers/inbox")]
    public async Task<ActionResult<List<HandoverDto>>> Inbox() => Ok(await handovers.InboxAsync(User.RequireCurrentUser()));

    [HttpPost("api/handovers")]
    public async Task<ActionResult<HandoverDto>> Create([FromBody] CreateHandoverRequest request) =>
        Ok(await handovers.CreateHandoverAsync(User.RequireCurrentUser(), request));

    [HttpPost("api/handovers/{id:long}/acknowledge")]
    public async Task<ActionResult<HandoverDto>> Acknowledge(long id) =>
        Ok(await handovers.AcknowledgeAsync(User.RequireCurrentUser(), id));
}
