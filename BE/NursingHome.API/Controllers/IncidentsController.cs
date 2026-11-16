using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.BusinessObjects.Enums;
using NursingHome.DTOs.Care;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>S3-H1 (UC-58, UC-59): báo cáo &amp; xử lý sự cố.</summary>
[ApiController]
[Route("api/incidents")]
[Authorize(Policy = AuthPolicies.ClinicalStaff)]
public class IncidentsController(IIncidentService incidents) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<IncidentDto>>> List(
        [FromQuery] int? residentId,
        [FromQuery] string? status,
        [FromQuery] string? severity,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to) =>
        Ok(await incidents.ListAsync(User.RequireCurrentUser(), residentId, status, severity, from, to));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<IncidentDto>> Get(int id) =>
        Ok(await incidents.GetAsync(User.RequireCurrentUser(), id));

    [HttpPost]
    public async Task<ActionResult<IncidentDto>> Report([FromBody] ReportIncidentRequest request) =>
        Ok(await incidents.ReportAsync(User.RequireCurrentUser(), request));

    /// <summary>Đổi trạng thái Open → Investigating → Closed; bắt buộc gửi RowVersion và kết luận khi đóng.</summary>
    [HttpPost("{id:int}/updates")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Nurse + "," + Roles.Doctor)]
    public async Task<ActionResult<IncidentDto>> Update(int id, [FromBody] UpdateIncidentRequest request) =>
        Ok(await incidents.UpdateAsync(User.RequireCurrentUser(), id, request));
}

/// <summary>S3-H1 (UC-107): sự cố đã chia sẻ hiển thị ở trang người thân.</summary>
[ApiController]
[Route("api/family/residents/{residentId:int}/incidents")]
[Authorize(Policy = AuthPolicies.FamilyOnly)]
public class FamilyIncidentsController(IIncidentService incidents) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FamilyIncidentDto>>> List(int residentId) =>
        Ok(await incidents.ListForFamilyAsync(User.RequireCurrentUser(), residentId));
}
