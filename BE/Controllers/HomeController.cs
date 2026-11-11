using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHomeManagement.Models;
using NursingHomeManagement.Services;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.Receptionist)]
public sealed class HomeController(DashboardService service) : Controller
{
    public async Task<IActionResult> Index() => View(await service.GetAsync());

    [AllowAnonymous]
    public async Task<IActionResult> Welcome() => View(await service.GetWelcomeAsync());

    [AllowAnonymous]
    public IActionResult Error() => View("~/Views/Shared/Error.cshtml", new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
}
