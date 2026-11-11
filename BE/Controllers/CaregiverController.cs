using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHomeManagement.Models;
using NursingHomeManagement.Services;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Controllers;

[Authorize(Roles = Roles.Caregiver)]
public sealed class CaregiverController(CareTaskService service, FacilityClock clock) : Controller
{
    public async Task<IActionResult> Index() => View(await service.ListAsync(clock.Today, null, 1));
}
