using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Reports;
using NursingHome.DTOs.Staffing;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>
/// BL-V4 nhân sự: hồ sơ & chứng chỉ (UC-43), chấm công (UC-48), hiệu suất (UC-126), trang chủ cá nhân (UC-127).
/// Hồ sơ chứa CCCD nên chỉ Admin xem/sửa người khác; nhân viên chỉ xem hồ sơ của chính mình.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Staff)]
public class StaffHrController(IStaffHrService staffHr, IAttendanceService attendance, IStaffHomeService home) : ControllerBase
{
    // ---------------- Hồ sơ & chứng chỉ ----------------

    [HttpGet("api/staff-profiles")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<List<StaffProfileSummaryDto>>> Profiles() => Ok(await staffHr.ListProfilesAsync());

    [HttpGet("api/staff-profiles/{accountId:int}")]
    public async Task<ActionResult<StaffProfileDto>> Profile(int accountId) => Ok(await staffHr.GetProfileAsync(User.RequireCurrentUser(), accountId));

    [HttpPut("api/staff-profiles/{accountId:int}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<StaffProfileDto>> SaveProfile(int accountId, [FromBody] SaveStaffProfileRequest request) =>
        Ok(await staffHr.SaveProfileAsync(accountId, request));

    [HttpPost("api/staff-profiles/{accountId:int}/certificates")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<StaffCertificateDto>> AddCertificate(int accountId, [FromBody] SaveCertificateRequest request) =>
        Ok(await staffHr.AddCertificateAsync(User.RequireCurrentUser(), accountId, request));

    [HttpPut("api/staff-certificates/{id:int}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<StaffCertificateDto>> UpdateCertificate(int id, [FromBody] SaveCertificateRequest request) =>
        Ok(await staffHr.UpdateCertificateAsync(id, request));

    [HttpDelete("api/staff-certificates/{id:int}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<IActionResult> DeleteCertificate(int id)
    {
        await staffHr.DeleteCertificateAsync(id);
        return NoContent();
    }

    [HttpGet("api/staff-certificates/expiring")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<List<StaffCertificateDto>>> Expiring() => Ok(await staffHr.ListExpiringAsync());

    /// <summary>Chạy ngay lượt cảnh báo (job nền vẫn chạy định kỳ). Mỗi mốc chỉ báo một lần.</summary>
    [HttpPost("api/staff-certificates/alerts/run")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<CertificateAlertResultDto>> RunAlerts(CancellationToken cancellationToken) =>
        Ok(await staffHr.SendExpiryAlertsAsync(cancellationToken));

    // ---------------- Chấm công ----------------

    [HttpGet("api/attendance/mine")]
    public async Task<ActionResult<List<MyShiftDto>>> MyShifts() => Ok(await attendance.MyShiftsAsync(User.RequireCurrentUser()));

    [HttpPost("api/attendance/check-in")]
    public async Task<ActionResult<MyShiftDto>> CheckIn([FromBody] CheckAttendanceRequest request) =>
        Ok(await attendance.CheckInAsync(User.RequireCurrentUser(), request.ShiftAssignmentId));

    [HttpPost("api/attendance/check-out")]
    public async Task<ActionResult<MyShiftDto>> CheckOut([FromBody] CheckAttendanceRequest request) =>
        Ok(await attendance.CheckOutAsync(User.RequireCurrentUser(), request.ShiftAssignmentId));

    [HttpGet("api/attendance")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<List<AttendanceRecordDto>>> Attendance([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? accountId) =>
        Ok(await attendance.ListAsync(from, to, accountId));

    /// <summary>Admin sửa hoặc bổ sung công (quên chấm công); bắt buộc lý do, lưu người sửa.</summary>
    [HttpPut("api/attendance/{shiftAssignmentId:int}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<AttendanceRecordDto>> EditAttendance(int shiftAssignmentId, [FromBody] EditAttendanceRequest request) =>
        Ok(await attendance.EditAsync(User.RequireCurrentUser(), shiftAssignmentId, request));

    // ---------------- Hiệu suất, trang chủ ----------------

    [HttpGet("api/reports/staff-performance")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<StaffPerformanceReportDto>> Performance([FromQuery] DateOnly? from, [FromQuery] DateOnly? to) =>
        Ok(await attendance.GetPerformanceAsync(from, to));

    [HttpGet("api/reports/staff-performance/export")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<IActionResult> ExportPerformance([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var file = await attendance.ExportPerformanceAsync(from, to);
        return File(file.Content, ExportFile.ContentType, file.FileName);
    }

    [HttpGet("api/staff-home")]
    public async Task<ActionResult<StaffHomeDto>> Home() => Ok(await home.GetAsync(User.RequireCurrentUser()));
}
