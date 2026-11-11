using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHomeManagement.Models;
using NursingHomeManagement.Services;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.Receptionist)]
public sealed class DashboardController(DashboardService service) : Controller
{
    public async Task<IActionResult> Index() => View(await service.GetAsync());
}
