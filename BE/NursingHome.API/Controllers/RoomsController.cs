using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Rooms;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>UC-34..UC-40: phòng, giường, phân/chuyển giường, lịch sử.</summary>
[ApiController]
[Route("api/rooms")]
[Authorize(Policy = AuthPolicies.Staff)]
public class RoomsController(IRoomService rooms) : ControllerBase
{
    /// <summary>Phòng + giường + cư dân đang nằm (sơ đồ phòng).</summary>
    [HttpGet]
    public async Task<ActionResult<List<RoomDto>>> List() => Ok(await rooms.ListAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RoomDto>> Get(int id) => Ok(await rooms.GetAsync(id));

    [HttpPost]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<RoomDto>> Create([FromBody] SaveRoomRequest request) => Ok(await rooms.SaveAsync(null, request));

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<RoomDto>> Update(int id, [FromBody] SaveRoomRequest request) => Ok(await rooms.SaveAsync(id, request));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<IActionResult> Delete(int id)
    {
        await rooms.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id:int}/beds")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<BedDto>> AddBed(int id, [FromBody] SaveBedRequest request) => Ok(await rooms.SaveBedAsync(id, null, request));

    [HttpPut("{id:int}/beds/{bedId:int}")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<BedDto>> UpdateBed(int id, int bedId, [FromBody] SaveBedRequest request) =>
        Ok(await rooms.SaveBedAsync(id, bedId, request));

    [HttpDelete("{id:int}/beds/{bedId:int}")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<IActionResult> DeleteBed(int id, int bedId)
    {
        await rooms.DeleteBedAsync(bedId);
        return NoContent();
    }

    /// <summary>Phân giường; nếu cư dân đang có giường thì chuyển (trả giường cũ trong cùng transaction).</summary>
    [HttpPost("~/api/bed-assignments")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<IActionResult> Assign([FromBody] AssignBedRequest request)
    {
        await rooms.AssignAsync(User.RequireCurrentUser(), request);
        return NoContent();
    }

    [HttpGet("~/api/bed-assignments")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<List<BedAssignmentDto>>> History([FromQuery] int? residentId) => Ok(await rooms.HistoryAsync(residentId));
}
