using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHomeManagement.Models;
using NursingHomeManagement.Services;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.Receptionist)]
public sealed class RoomsController(RoomService service) : WorkflowController
{
    public async Task<IActionResult> Index(string? search, int page = 1) => View(await service.ListAsync(search, page));
    public async Task<IActionResult> Details(int id) => View(await service.GetAsync(id));
    public IActionResult Create() => View("Form", new RoomForm());
    [HttpPost]
    public async Task<IActionResult> Create(RoomForm form)
    {
        return await TrySaveAsync(() => service.SaveAsync(null, form)) ? RedirectToAction(nameof(Index)) : View("Form", form);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var room = await service.GetAsync(id);
        return View("Form", new RoomForm { Number = room.Number, Description = room.Description, IsActive = room.IsActive });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, RoomForm form)
    {
        return await TrySaveAsync(() => service.SaveAsync(id, form)) ? RedirectToAction(nameof(Details), new
        {
            id
        }) : View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        return await TrySaveAsync(() => service.DeleteAsync(id)) ? RedirectToAction(nameof(Index)) : View("Operation");
    }
}
