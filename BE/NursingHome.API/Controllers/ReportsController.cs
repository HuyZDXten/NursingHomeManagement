using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Security;
using NursingHome.DTOs.Reports;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>
/// MODULE VẬN HÀNH (Viên) — S3-V3 báo cáo. Mỗi báo cáo: JSON + /export (.xlsx). Policy theo từng action.
/// UC-125 sự cố (CarePlanners) thêm sau khi S3-H1 có bảng Incidents.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController(IReportService reports) : ControllerBase
{
    [HttpGet("revenue")]
    [Authorize(Policy = AuthPolicies.FinanceStaff)]
    public async Task<ActionResult<RevenueReportDto>> Revenue([FromQuery] int? year) => Ok(await reports.GetRevenueAsync(year));

    [HttpGet("revenue/export")]
    [Authorize(Policy = AuthPolicies.FinanceStaff)]
    public async Task<IActionResult> ExportRevenue([FromQuery] int? year) => ToFile(await reports.ExportRevenueAsync(year));

    [HttpGet("occupancy")]
    [Authorize(Policy = AuthPolicies.BillingStaff)]
    public async Task<ActionResult<OccupancyReportDto>> Occupancy([FromQuery] int? year) => Ok(await reports.GetOccupancyAsync(year));

    [HttpGet("occupancy/export")]
    [Authorize(Policy = AuthPolicies.BillingStaff)]
    public async Task<IActionResult> ExportOccupancy([FromQuery] int? year) => ToFile(await reports.ExportOccupancyAsync(year));

    /// <param name="from">yyyy-MM-dd, mặc định 30 ngày trước <paramref name="to"/>.</param>
    /// <param name="to">yyyy-MM-dd, mặc định hôm nay.</param>
    [HttpGet("medication-adherence")]
    [Authorize(Policy = AuthPolicies.Prescribers)]
    public async Task<ActionResult<AdherenceReportDto>> Adherence([FromQuery] DateOnly? from, [FromQuery] DateOnly? to) =>
        Ok(await reports.GetAdherenceAsync(from, to));

    [HttpGet("medication-adherence/export")]
    [Authorize(Policy = AuthPolicies.Prescribers)]
    public async Task<IActionResult> ExportAdherence([FromQuery] DateOnly? from, [FromQuery] DateOnly? to) =>
        ToFile(await reports.ExportAdherenceAsync(from, to));

    private FileContentResult ToFile(ExportFile file) => File(file.Content, ExportFile.ContentType, file.FileName);
}
