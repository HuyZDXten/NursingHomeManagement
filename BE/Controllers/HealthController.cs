using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHomeManagement.Models;
using NursingHomeManagement.Services;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.Caregiver)]
public sealed class HealthController(HealthService service, LookupService lookups) : WorkflowController
{
    public async Task<IActionResult> Index(int residentId, DateOnly? date, int page = 1)
    {
        ViewData["ResidentId"] = residentId;
        return View(await service.ListAsync(residentId, date, page));
    }

    public async Task<IActionResult> Create(int residentId)
    {
        ViewData["ResidentIdOptions"] = await lookups.ResidentsAsync();
        return View("Form", new HealthForm { ResidentId = residentId });
    }

    [HttpPost]
    public async Task<IActionResult> Create(HealthForm form)
    {
        if (await TrySaveAsync(() => service.RecordAsync(form)))
        {
            return RedirectToAction(nameof(Index), new
            {
                residentId = form.ResidentId
            });
        }

        ViewData["ResidentIdOptions"] = await lookups.ResidentsAsync();
        return View("Form", form);
    }
}
