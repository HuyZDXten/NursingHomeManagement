using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Care;
using NursingHome.DTOs.Medication;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>UC-50..UC-55 công việc chăm sóc. Danh sách: GET /odata/CareTasks?date=yyyy-MM-dd&amp;mine=true.</summary>
[ApiController]
[Route("api/care-tasks")]
[Authorize(Policy = AuthPolicies.ClinicalStaff)]
public class CareTasksController(ICareTaskService care) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CareTaskDto>> Get(int id) => Ok(await care.GetAsync(User.RequireCurrentUser(), id));

    [HttpPost]
    [Authorize(Policy = AuthPolicies.CarePlanners)]
    public async Task<ActionResult<CareTaskDto>> Create([FromBody] CreateCareTaskRequest request) =>
        Ok(await care.CreateAsync(User.RequireCurrentUser(), request));

    /// <summary>Cập nhật trạng thái + kết quả; gửi kèm RowVersion vừa đọc để chống ghi đè.</summary>
    [HttpPost("{id:int}/updates")]
    public async Task<ActionResult<CareTaskDto>> Update(int id, [FromBody] UpdateCareTaskRequest request) =>
        Ok(await care.UpdateAsync(User.RequireCurrentUser(), id, request));

    [HttpGet("{id:int}/records")]
    public async Task<ActionResult<List<CareRecordDto>>> Records(int id) => Ok(await care.ListRecordsAsync(User.RequireCurrentUser(), id));
}

/// <summary>UC-62, UC-65 ghi sinh hiệu (danh sách theo cư dân: GET /api/residents/{id}/vitals).</summary>
[ApiController]
[Route("api/vitals")]
[Authorize(Policy = AuthPolicies.ClinicalStaff)]
public class VitalsController(IHealthService health) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<VitalSignDto>> Record([FromBody] RecordVitalSignRequest request) =>
        Ok(await health.RecordAsync(User.RequireCurrentUser(), request));
}

/// <summary>UC-73..UC-78 lịch thuốc và bảng phát thuốc.</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.ClinicalStaff)]
public class MedicationsController(IMedicationService medications) : ControllerBase
{
    [HttpGet("api/medication-schedules")]
    public async Task<ActionResult<List<MedicationScheduleDto>>> Schedules([FromQuery] int? residentId) =>
        Ok(await medications.ListSchedulesAsync(User.RequireCurrentUser(), residentId));

    [HttpPost("api/medication-schedules")]
    [Authorize(Policy = AuthPolicies.Prescribers)]
    public async Task<ActionResult<MedicationScheduleDto>> Create([FromBody] CreateMedicationScheduleRequest request) =>
        Ok(await medications.CreateScheduleAsync(User.RequireCurrentUser(), request));

    /// <summary>Đổi chỉ định = dừng lịch cũ rồi tạo lịch mới (giữ lịch sử).</summary>
    [HttpPost("api/medication-schedules/{id:int}/stop")]
    [Authorize(Policy = AuthPolicies.Prescribers)]
    public async Task<IActionResult> Stop(int id)
    {
        await medications.StopAsync(id);
        return NoContent();
    }

    [HttpGet("api/medications/today")]
    public async Task<ActionResult<List<MedicationDoseDto>>> Today([FromQuery] DateOnly? date) =>
        Ok(await medications.TodayAsync(User.RequireCurrentUser(), date));

    [HttpPost("api/medication-logs")]
    public async Task<IActionResult> Record([FromBody] RecordMedicationRequest request)
    {
        await medications.RecordAsync(User.RequireCurrentUser(), request);
        return NoContent();
    }
}
