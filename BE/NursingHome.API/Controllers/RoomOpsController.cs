using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Rooms;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>BL-V3: loại phòng & phụ thu (UC-36), giữ giường (UC-41), báo hỏng & sửa chữa (UC-42).</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Staff)]
public class RoomOpsController(IRoomOpsService roomOps) : ControllerBase
{
    // ---------------- Loại phòng: xem mọi nhân viên, sửa Admin ----------------

    [HttpGet("api/room-types")]
    public async Task<ActionResult<List<RoomTypeDto>>> RoomTypes([FromQuery] bool activeOnly = false) => Ok(await roomOps.ListTypesAsync(activeOnly));

    [HttpPost("api/room-types")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<RoomTypeDto>> CreateRoomType([FromBody] SaveRoomTypeRequest request) => Ok(await roomOps.SaveTypeAsync(null, request));

    [HttpPut("api/room-types/{id:int}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<RoomTypeDto>> UpdateRoomType(int id, [FromBody] SaveRoomTypeRequest request) => Ok(await roomOps.SaveTypeAsync(id, request));

    // ---------------- Giữ giường: lễ tân, Admin ----------------

    [HttpGet("api/bed-reservations")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<List<BedReservationDto>>> Reservations([FromQuery] bool activeOnly = false) => Ok(await roomOps.ListReservationsAsync(activeOnly));

    [HttpPost("api/bed-reservations")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<BedReservationDto>> Reserve([FromBody] CreateBedReservationRequest request) =>
        Ok(await roomOps.CreateReservationAsync(User.RequireCurrentUser(), request));

    [HttpPost("api/bed-reservations/{id:int}/release")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<BedReservationDto>> Release(int id) => Ok(await roomOps.ReleaseReservationAsync(User.RequireCurrentUser(), id));

    // ---------------- Báo hỏng: mọi nhân viên báo, Admin xử lý ----------------

    [HttpGet("api/maintenance-requests")]
    public async Task<ActionResult<List<MaintenanceRequestDto>>> Maintenance([FromQuery] string? status, [FromQuery] int? roomId) =>
        Ok(await roomOps.ListMaintenanceAsync(status, roomId));

    [HttpPost("api/maintenance-requests")]
    public async Task<ActionResult<MaintenanceRequestDto>> Report([FromBody] CreateMaintenanceRequest request) =>
        Ok(await roomOps.CreateMaintenanceAsync(User.RequireCurrentUser(), request));

    [HttpPost("api/maintenance-requests/{id:int}/status")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<MaintenanceRequestDto>> UpdateStatus(int id, [FromBody] UpdateMaintenanceRequest request) =>
        Ok(await roomOps.UpdateMaintenanceAsync(User.RequireCurrentUser(), id, request));
}
