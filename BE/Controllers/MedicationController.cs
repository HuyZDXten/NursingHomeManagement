using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHomeManagement.Models;
using NursingHomeManagement.Services;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.Caregiver)]
public sealed class MedicationController(
    MedicationService service,
    LookupService lookups,
    FacilityClock clock) : WorkflowController
{
    public async Task<IActionResult> Index(int? residentId, int page = 1) => View(await service.ListAsync(residentId, page));
    public async Task<IActionResult> Today(DateOnly? date, int page = 1) => View(await service.TodayAsync(date ?? clock.Today, page));
    public async Task<IActionResult> History(int residentId, int page = 1) => View(await service.HistoryAsync(residentId, page));
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(int residentId)
    {
        ViewData["ResidentIdOptions"] = await lookups.ResidentsAsync();
        return View("Form", new MedicationForm { ResidentId = residentId });
    }

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(MedicationForm form)
    {
        if (await TrySaveAsync(() => service.CreateAsync(form)))
        {
            return RedirectToAction(nameof(Index));
        }

        ViewData["ResidentIdOptions"] = await lookups.ResidentsAsync();
        return View("Form", form);
    }

    public IActionResult Record(int scheduleId, DateOnly date) => View("Form", new MedicationLogForm { ScheduleId = scheduleId, Date = date, Status = MedicationStatus.Taken });
    [HttpPost]
    public async Task<IActionResult> Record(MedicationLogForm form)
    {
        return await TrySaveAsync(() => service.RecordAsync(form)) ? RedirectToAction(nameof(Today), new
        {
            date = form.Date.ToString("yyyy-MM-dd")
        }) : View("Form", form);
    }

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Stop(int id)
    {
        return await TrySaveAsync(() => service.StopAsync(id)) ? RedirectToAction(nameof(Index)) : View("Operation");
    }
}
