using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Care;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>S3-H4 (UC-51): kế hoạch chăm sóc &amp; sinh việc tự động.</summary>
[ApiController]
public class CarePlansController(ICarePlanService carePlans) : ControllerBase
{
    [HttpGet("api/residents/{residentId:int}/care-plans")]
    [Authorize(Policy = AuthPolicies.ClinicalStaff)]
    public async Task<ActionResult<List<CarePlanDto>>> ListByResident(int residentId) =>
        Ok(await carePlans.ListByResidentAsync(User.RequireCurrentUser(), residentId));

    [HttpPost("api/care-plans")]
    [Authorize(Policy = AuthPolicies.CarePlanners)]
    public async Task<ActionResult<CarePlanDto>> Create([FromBody] SaveCarePlanRequest request) =>
        Ok(await carePlans.CreateAsync(User.RequireCurrentUser(), request));

    [HttpPut("api/care-plans/{id:int}")]
    [Authorize(Policy = AuthPolicies.CarePlanners)]
    public async Task<ActionResult<CarePlanDto>> Update(int id, [FromBody] SaveCarePlanRequest request) =>
        Ok(await carePlans.UpdateAsync(User.RequireCurrentUser(), id, request));

    [HttpPost("api/care-plans/{id:int}/deactivate")]
    [Authorize(Policy = AuthPolicies.CarePlanners)]
    public async Task<IActionResult> Deactivate(int id)
    {
        await carePlans.DeactivateAsync(id);
        return NoContent();
    }

    [HttpPost("api/care-plans/generate")]
    [Authorize(Policy = AuthPolicies.CarePlanners)]
    public async Task<ActionResult<GenerateTasksResultDto>> Generate([FromQuery] int days = 2, [FromQuery] int? residentId = null) =>
        Ok(await carePlans.GenerateForDatesAsync(days, residentId));
}

/// <summary>S3-H4 (UC-56, UC-57): ghi nhận ADL và bữa ăn hằng ngày.</summary>
[ApiController]
[Route("api/residents/{residentId:int}")]
[Authorize(Policy = AuthPolicies.ClinicalStaff)]
public class DailyLogsController(IDailyLogService dailyLogs, FacilityClock clock) : ControllerBase
{
    [HttpGet("daily-logs")]
    public async Task<ActionResult<List<DailyCareLogDto>>> ListLogs(int residentId, [FromQuery] DateOnly? date) =>
        Ok(await dailyLogs.ListLogsAsync(User.RequireCurrentUser(), residentId, date ?? clock.Today));

    [HttpPost("daily-logs")]
    public async Task<ActionResult<DailyCareLogDto>> AddLog(int residentId, [FromBody] CreateDailyCareLogRequest request) =>
        Ok(await dailyLogs.AddLogAsync(User.RequireCurrentUser(), residentId, request));

    [HttpGet("meals")]
    public async Task<ActionResult<List<MealLogDto>>> ListMeals(int residentId, [FromQuery] DateOnly? date) =>
        Ok(await dailyLogs.ListMealsAsync(User.RequireCurrentUser(), residentId, date ?? clock.Today));

    [HttpPut("meals")]
    public async Task<ActionResult<MealLogDto>> UpsertMeal(int residentId, [FromBody] UpsertMealLogRequest request) =>
        Ok(await dailyLogs.UpsertMealAsync(User.RequireCurrentUser(), residentId, request));
}
