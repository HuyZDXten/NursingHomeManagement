using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHomeManagement.Models;
using NursingHomeManagement.Services;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Controllers;

[Authorize(Roles = Roles.Admin)]
public sealed class UsersController(UserAccountService service) : WorkflowController
{
    public async Task<IActionResult> Index(string? search, int page = 1) => View(await service.ListAsync(search, page));
    public IActionResult Create() => View("Form", new UserForm());
    public async Task<IActionResult> Edit(string id) => View("Form", await service.GetAsync(id));
    [HttpPost]
    public async Task<IActionResult> Create(UserForm form)
    {
        return await TrySaveAsync(() => service.CreateAsync(form)) ? RedirectToAction(nameof(Index)) : View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(string id, UserEditForm form)
    {
        return await TrySaveAsync(() => service.EditAsync(id, form)) ? RedirectToAction(nameof(Index)) : View("Form", form);
    }
}
