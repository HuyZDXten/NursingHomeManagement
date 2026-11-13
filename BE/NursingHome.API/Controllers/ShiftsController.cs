using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Staffing;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>
/// MODULE VẬN HÀNH (Viên) — S3-V2: UC-44 lịch ca (Admin xếp), UC-45 phân công phụ trách (Admin/Nurse), UC-46 lịch ca của tôi.
/// Policy đặt theo từng action (policy ở class và action cộng dồn AND).
/// </summary>
[ApiController]
[Authorize]
public class ShiftsController(IStaffingService staffing) : ControllerBase
{
    [HttpGet("api/shifts")]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<ActionResult<List<ShiftDto>>> Shifts() => Ok(await staffing.ListShiftsAsync());

    /// <param name="from">yyyy-MM-dd, mặc định thứ Hai tuần này.</param>
    /// <param name="to">yyyy-MM-dd, mặc định from + 6 ngày (tối đa 62 ngày).</param>
    [HttpGet("api/shift-assignments")]
    [Authorize(Policy = AuthPolicies.ClinicalStaff)]
    public async Task<ActionResult<List<ShiftAssignmentDto>>> Assignments([FromQuery] DateOnly? from, [FromQuery] DateOnly? to) =>
        Ok(await staffing.ListAssignmentsAsync(User.RequireCurrentUser(), from, to));

    [HttpGet("api/shift-assignments/mine")]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<ActionResult<List<ShiftAssignmentDto>>> Mine([FromQuery] int days = 14) =>
        Ok(await staffing.MyAssignmentsAsync(User.RequireCurrentUser(), days));

    [HttpPost("api/shift-assignments")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<ShiftAssignmentDto>> Create([FromBody] CreateShiftAssignmentRequest request) =>
        Ok(await staffing.CreateAssignmentAsync(User.RequireCurrentUser(), request));

    [HttpDelete("api/shift-assignments/{id:int}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<IActionResult> Delete(int id)
    {
        await staffing.DeleteAssignmentAsync(id);
        return NoContent();
    }

    [HttpPost("api/shift-assignments/copy-week")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<CopyWeekResultDto>> CopyWeek([FromBody] CopyWeekRequest request) =>
        Ok(await staffing.CopyWeekAsync(User.RequireCurrentUser(), request));

    /// <param name="activeOnly">false = cả phân công đã kết thúc (lịch sử).</param>
    [HttpGet("api/care-assignments")]
    [Authorize(Policy = AuthPolicies.ClinicalStaff)]
    public async Task<ActionResult<List<CareAssignmentDto>>> CareAssignments(
        [FromQuery] int? residentId, [FromQuery] int? accountId, [FromQuery] bool activeOnly = true) =>
        Ok(await staffing.ListCareAssignmentsAsync(User.RequireCurrentUser(), residentId, accountId, activeOnly));

    [HttpPost("api/care-assignments")]
    [Authorize(Policy = AuthPolicies.CarePlanners)]
    public async Task<ActionResult<CareAssignmentDto>> CreateCareAssignment([FromBody] CreateCareAssignmentRequest request) =>
        Ok(await staffing.CreateCareAssignmentAsync(User.RequireCurrentUser(), request));

    [HttpPost("api/care-assignments/{id:int}/end")]
    [Authorize(Policy = AuthPolicies.CarePlanners)]
    public async Task<ActionResult<CareAssignmentDto>> EndCareAssignment(int id) => Ok(await staffing.EndCareAssignmentAsync(id));
}
