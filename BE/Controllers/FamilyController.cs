using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHomeManagement.Models;
using NursingHomeManagement.Services;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Controllers;

[Authorize(Roles = Roles.Family)]
public sealed class FamilyController(FamilyService service) : Controller
{
    public async Task<IActionResult> Index() => View(await service.ListAsync());
    public async Task<IActionResult> Details(int id, int page = 1) => View(await service.DetailsAsync(id, page));
}
