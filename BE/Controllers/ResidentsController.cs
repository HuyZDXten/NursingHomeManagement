using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHomeManagement.Models;
using NursingHomeManagement.Services;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.Receptionist + "," + Roles.Caregiver)]
public sealed class ResidentsController(ResidentService service, LookupService lookups) : WorkflowController
{
    public async Task<IActionResult> Index(string? search, ResidentStatus? status, int page = 1)
    {
        return View(await service.ListAsync(search, status, page));
    }

    public async Task<IActionResult> Details(int id)
    {
        var resident = await service.GetAsync(id);
        if (!User.IsInRole(Roles.Caregiver))
        {
            ViewData["Contacts"] = await service.ContactsAsync(id);
            ViewData["Links"] = await service.LinksAsync(id);
        }

        return View(resident);
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Receptionist)]
    public IActionResult Create() => View("Form", new ResidentForm());
    [HttpPost, Authorize(Roles = Roles.Admin + "," + Roles.Receptionist)]
    public async Task<IActionResult> Create(ResidentForm form)
    {
        int id = 0;
        if (await TrySaveAsync(async () => id = await service.SaveAsync(null, form)))
        {
            return RedirectToAction(nameof(Details), new
            {
                id
            });
        }

        return View("Form", form);
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Receptionist)]
    public async Task<IActionResult> Edit(int id)
    {
        var resident = await service.GetAsync(id);
        return View("Form", new ResidentForm { Code = resident.Code, FullName = resident.FullName, DateOfBirth = resident.DateOfBirth, Gender = resident.Gender, Notes = resident.Notes });
    }

    [HttpPost, Authorize(Roles = Roles.Admin + "," + Roles.Receptionist)]
    public async Task<IActionResult> Edit(int id, ResidentForm form)
    {
        if (await TrySaveAsync(() => service.SaveAsync(id, form)))
        {
            return RedirectToAction(nameof(Details), new
            {
                id
            });
        }

        return View("Form", form);
    }

    [HttpPost, Authorize(Roles = Roles.Admin + "," + Roles.Receptionist)]
    public async Task<IActionResult> Discharge(int id)
    {
        return await TrySaveAsync(() => service.DischargeAsync(id)) ? RedirectToAction(nameof(Details), new
        {
            id
        }) : View("Operation");
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Receptionist)]
    public async Task<IActionResult> Contact(int residentId, int? id)
    {
        await service.GetAsync(residentId);
        var contact = (await service.ContactsAsync(residentId)).SingleOrDefault(item => item.Id == id);
        if (id.HasValue && contact is null)
        {
            return NotFound();
        }

        return View("Form", new ContactForm { ResidentId = residentId, FullName = contact?.FullName ?? "", Relationship = contact?.Relationship ?? "", Phone = contact?.Phone ?? "", Email = contact?.Email, IsEmergencyContact = contact?.IsEmergencyContact ?? false });
    }

    [HttpPost, Authorize(Roles = Roles.Admin + "," + Roles.Receptionist)]
    public async Task<IActionResult> Contact(int? id, ContactForm form)
    {
        if (await TrySaveAsync(() => service.SaveContactAsync(id, form)))
        {
            return RedirectToAction(nameof(Details), new
            {
                id = form.ResidentId
            });
        }

        return View("Form", form);
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Receptionist)]
    public async Task<IActionResult> Link(int residentId, string? familyUserId)
    {
        await service.GetAsync(residentId);
        ViewData["FamilyUserIdOptions"] = await lookups.UsersAsync(Roles.Family);
        var link = (await service.LinksAsync(residentId)).SingleOrDefault(item => item.FamilyUserId == familyUserId);
        return View("Form", new FamilyLinkForm
        {
            ResidentId = residentId,
            FamilyUserId = link?.FamilyUserId ?? "",
            CanViewHealth = link?.CanViewHealth ?? false,
            CanViewCare = link?.CanViewCare ?? false,
            CanViewMedication = link?.CanViewMedication ?? false,
            CanViewBilling = link?.CanViewBilling ?? false
        });
    }

    [HttpPost, Authorize(Roles = Roles.Admin + "," + Roles.Receptionist)]
    public async Task<IActionResult> Link(FamilyLinkForm form)
    {
        if (await TrySaveAsync(() => service.LinkAsync(form)))
        {
            return RedirectToAction(nameof(Details), new
            {
                id = form.ResidentId
            });
        }

        ViewData["FamilyUserIdOptions"] = await lookups.UsersAsync(Roles.Family);
        return View("Form", form);
    }

    [HttpPost, Authorize(Roles = Roles.Admin + "," + Roles.Receptionist)]
    public async Task<IActionResult> Unlink(int id)
    {
        return await TrySaveAsync(() => service.UnlinkAsync(id)) ? RedirectToAction(nameof(Index)) : View("Operation");
    }
}
