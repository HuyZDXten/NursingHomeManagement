using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHomeManagement.Models;
using NursingHomeManagement.Services;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.Caregiver)]
public sealed class CareTasksController(CareTaskService service, LookupService lookups) : WorkflowController
{
    public async Task<IActionResult> Index(DateOnly? date, CareTaskStatus? status, int page = 1) => View(await service.ListAsync(date, status, page));
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create()
    {
        await LoadOptionsAsync();
        return View("Form", new CareTaskForm());
    }

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(CareTaskForm form)
    {
        if (await TrySaveAsync(() => service.CreateAsync(form)))
        {
            return RedirectToAction(nameof(Index));
        }

        await LoadOptionsAsync();
        return View("Form", form);
    }

    public async Task<IActionResult> Update(int id)
    {
        var task = await service.GetAsync(id);
        ViewData["Title"] = task.Title;
        return View("Form", new CareUpdateForm { Status = CareTaskStatus.InProgress, RowVersion = Convert.ToBase64String(task.RowVersion) });
    }

    [HttpPost]
    public async Task<IActionResult> Update(int id, CareUpdateForm form)
    {
        return await TrySaveAsync(() => service.UpdateAsync(id, form)) ? RedirectToAction(nameof(History), new
        {
            id
        }) : View("Form", form);
    }

    public async Task<IActionResult> History(int id)
    {
        ViewData["Task"] = await service.GetAsync(id);
        return View(await service.HistoryAsync(id));
    }

    private async Task LoadOptionsAsync()
    {
        ViewData["ResidentIdOptions"] = await lookups.ResidentsAsync();
        ViewData["AssignedToIdOptions"] = await lookups.UsersAsync(Roles.Caregiver);
    }
}
