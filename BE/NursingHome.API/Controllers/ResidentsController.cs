using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Billing;
using NursingHome.DTOs.Care;
using NursingHome.DTOs.Medication;
using NursingHome.DTOs.Residents;
using NursingHome.DTOs.Rooms;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>
/// UC-23..UC-28, UC-32 và các phần con theo cư dân. Danh sách: GET /odata/Residents.
/// </summary>
[ApiController]
[Route("api/residents")]
[Authorize(Policy = AuthPolicies.Staff)]
public class ResidentsController(
    IResidentService residents,
    IRoomService rooms,
    IHealthService health,
    IMedicationService medications,
    IBillingService billing) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ResidentDetailDto>> Get(int id) => Ok(await residents.GetAsync(User.RequireCurrentUser(), id));

    [HttpPost]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<ResidentDetailDto>> Create([FromBody] SaveResidentRequest request)
    {
        var created = await residents.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<ResidentDetailDto>> Update(int id, [FromBody] SaveResidentRequest request) =>
        Ok(await residents.UpdateAsync(id, request));

    [HttpPost("{id:int}/discharge")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<IActionResult> Discharge(int id, [FromBody] DischargeRequest request)
    {
        await residents.DischargeAsync(User.RequireCurrentUser(), id, request);
        return NoContent();
    }

    // ---------------- Người liên hệ ----------------

    [HttpGet("{id:int}/contacts")]
    public async Task<ActionResult<List<ContactDto>>> Contacts(int id) => Ok(await residents.ListContactsAsync(User.RequireCurrentUser(), id));

    [HttpPost("{id:int}/contacts")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<ContactDto>> AddContact(int id, [FromBody] SaveContactRequest request) =>
        Ok(await residents.SaveContactAsync(id, null, request));

    [HttpPut("{id:int}/contacts/{contactId:int}")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<ContactDto>> UpdateContact(int id, int contactId, [FromBody] SaveContactRequest request) =>
        Ok(await residents.SaveContactAsync(id, contactId, request));

    [HttpDelete("{id:int}/contacts/{contactId:int}")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<IActionResult> DeleteContact(int id, int contactId)
    {
        await residents.DeleteContactAsync(id, contactId);
        return NoContent();
    }

    // ---------------- Liên kết Family ----------------

    [HttpGet("{id:int}/family-links")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<List<FamilyLinkDto>>> FamilyLinks(int id) => Ok(await residents.ListLinksAsync(id));

    /// <summary>Tạo mới hoặc sửa quyền chia sẻ của một tài khoản Family.</summary>
    [HttpPut("{id:int}/family-links")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<FamilyLinkDto>> SaveFamilyLink(int id, [FromBody] SaveFamilyLinkRequest request) =>
        Ok(await residents.SaveLinkAsync(id, request));

    [HttpDelete("{id:int}/family-links/{linkId:int}")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<IActionResult> DeleteFamilyLink(int id, int linkId)
    {
        await residents.DeleteLinkAsync(id, linkId);
        return NoContent();
    }

    // ---------------- Giường ----------------

    [HttpPost("{id:int}/release-bed")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<IActionResult> ReleaseBed(int id)
    {
        await rooms.ReleaseAsync(id);
        return NoContent();
    }

    [HttpGet("{id:int}/bed-history")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<List<BedAssignmentDto>>> BedHistory(int id) => Ok(await rooms.HistoryAsync(id));

    // ---------------- Sức khỏe, thuốc, gói ----------------

    [HttpGet("{id:int}/vitals")]
    [Authorize(Policy = AuthPolicies.ClinicalStaff)]
    public async Task<ActionResult<List<VitalSignDto>>> Vitals(int id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to) =>
        Ok(await health.ListAsync(User.RequireCurrentUser(), id, from, to));

    [HttpGet("{id:int}/medication-logs")]
    [Authorize(Policy = AuthPolicies.ClinicalStaff)]
    public async Task<ActionResult<List<MedicationLogDto>>> MedicationLogs(int id) =>
        Ok(await medications.HistoryAsync(User.RequireCurrentUser(), id));

    [HttpGet("{id:int}/subscriptions")]
    [Authorize(Policy = AuthPolicies.BillingStaff)]
    public async Task<ActionResult<List<SubscriptionDto>>> Subscriptions(int id) => Ok(await billing.ListSubscriptionsAsync(id));
}
