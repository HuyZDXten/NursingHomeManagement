using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHomeManagement.Models;
using NursingHomeManagement.Services;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.Receptionist)]
public sealed class BedsController(RoomService service, LookupService lookups) : WorkflowController
{
    public async Task<IActionResult> Create(int roomId)
    {
        ViewData["RoomIdOptions"] = await lookups.RoomsAsync();
        return View("Form", new BedForm { RoomId = roomId });
    }

    [HttpPost]
    public async Task<IActionResult> Create(BedForm form)
    {
        if (await TrySaveAsync(() => service.SaveBedAsync(null, form)))
        {
            return RedirectToAction("Details", "Rooms", new
            {
                id = form.RoomId
            });
        }

        ViewData["RoomIdOptions"] = await lookups.RoomsAsync();
        return View("Form", form);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var bed = await service.BedAsync(id);
        ViewData["RoomIdOptions"] = await lookups.RoomsAsync();
        return View("Form", new BedForm { RoomId = bed.RoomId, Number = bed.Number, Maintenance = bed.Status == BedStatus.Maintenance });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, BedForm form)
    {
        if (await TrySaveAsync(() => service.SaveBedAsync(id, form)))
        {
            return RedirectToAction("Details", "Rooms", new
            {
                id = form.RoomId
            });
        }

        ViewData["RoomIdOptions"] = await lookups.RoomsAsync();
        return View("Form", form);
    }

    public async Task<IActionResult> Assign(int residentId)
    {
        await LoadAssignmentsAsync();
        return View("Form", new BedAssignmentForm { ResidentId = residentId });
    }

    [HttpPost]
    public async Task<IActionResult> Assign(BedAssignmentForm form)
    {
        if (await TrySaveAsync(() => service.AssignAsync(form)))
        {
            return RedirectToAction(nameof(History), new
            {
                residentId = form.ResidentId
            });
        }

        await LoadAssignmentsAsync();
        return View("Form", form);
    }

    public async Task<IActionResult> History(int? residentId, int page = 1) => View(await service.HistoryAsync(residentId, page));
    [HttpPost]
    public async Task<IActionResult> Release(int residentId)
    {
        return await TrySaveAsync(() => service.ReleaseAsync(residentId)) ? RedirectToAction(nameof(History), new
        {
            residentId
        }) : View("Operation");
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        return await TrySaveAsync(() => service.DeleteBedAsync(id)) ? RedirectToAction("Index", "Rooms") : View("Operation");
    }

    private async Task LoadAssignmentsAsync()
    {
        ViewData["ResidentIdOptions"] = await lookups.ResidentsAsync();
        ViewData["BedIdOptions"] = await lookups.BedsAsync();
    }
}
