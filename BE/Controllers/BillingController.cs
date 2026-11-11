using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHomeManagement.Models;
using NursingHomeManagement.Services;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.Receptionist)]
public sealed class BillingController(BillingService service, LookupService lookups) : WorkflowController
{
    public async Task<IActionResult> Index(string? search, InvoiceStatus? status, int page = 1) => View(await service.ListAsync(search, status, page));
    public async Task<IActionResult> Details(int id) => View(await service.GetAsync(id));
    public async Task<IActionResult> Packages(string? search, int page = 1) => View(await service.PackagesAsync(search, page));
    public async Task<IActionResult> Subscriptions(int? residentId, int page = 1) => View(await service.SubscriptionsAsync(residentId, page));
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Package(int? id)
    {
        if (!id.HasValue)
        {
            return View("Form", new PackageForm());
        }

        var package = await service.PackageAsync(id.Value);
        return View("Form", new PackageForm { Name = package.Name, MonthlyPrice = package.MonthlyPrice, Description = package.Description, IsActive = package.IsActive });
    }

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Package(int? id, PackageForm form)
    {
        return await TrySaveAsync(() => service.SavePackageAsync(id, form)) ? RedirectToAction(nameof(Packages)) : View("Form", form);
    }

    public async Task<IActionResult> Subscribe(int residentId)
    {
        await LoadSubscriptionOptionsAsync();
        return View("Form", new SubscriptionForm { ResidentId = residentId });
    }

    [HttpPost]
    public async Task<IActionResult> Subscribe(SubscriptionForm form)
    {
        if (await TrySaveAsync(() => service.SubscribeAsync(form)))
        {
            return RedirectToAction(nameof(Subscriptions));
        }

        await LoadSubscriptionOptionsAsync();
        return View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> EndSubscription(int id)
    {
        return await TrySaveAsync(() => service.EndSubscriptionAsync(id)) ? RedirectToAction(nameof(Subscriptions)) : View("Operation");
    }

    public async Task<IActionResult> Create(int residentId)
    {
        ViewData["ResidentIdOptions"] = await lookups.ResidentsAsync();
        return View("Form", new InvoiceForm { ResidentId = residentId });
    }

    [HttpPost]
    public async Task<IActionResult> Create(InvoiceForm form)
    {
        int id = 0;
        if (await TrySaveAsync(async () => id = await service.CreateAsync(form)))
        {
            return RedirectToAction(nameof(Details), new
            {
                id
            });
        }

        ViewData["ResidentIdOptions"] = await lookups.ResidentsAsync();
        return View("Form", form);
    }

    public async Task<IActionResult> AddItem(int id)
    {
        await service.GetAsync(id);
        return View("Form", new InvoiceItemForm());
    }

    [HttpPost]
    public async Task<IActionResult> AddItem(int id, InvoiceItemForm form)
    {
        return await TrySaveAsync(() => service.AddItemAsync(id, form)) ? RedirectToAction(nameof(Details), new
        {
            id
        }) : View("Form", form);
    }

    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Pay(int id)
    {
        var invoice = await service.GetAsync(id);
        return View("Form", new PaymentForm { Amount = BillingService.Total(invoice) - invoice.Payments.Sum(payment => payment.Amount) });
    }

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Pay(int id, PaymentForm form)
    {
        return await TrySaveAsync(() => service.PayAsync(id, form)) ? RedirectToAction(nameof(Details), new
        {
            id
        }) : View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> Issue(int id)
    {
        return await TrySaveAsync(() => service.IssueAsync(id)) ? RedirectToAction(nameof(Details), new
        {
            id
        }) : View("Operation");
    }

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Cancel(int id)
    {
        return await TrySaveAsync(() => service.CancelAsync(id)) ? RedirectToAction(nameof(Details), new
        {
            id
        }) : View("Operation");
    }

    private async Task LoadSubscriptionOptionsAsync()
    {
        ViewData["ResidentIdOptions"] = await lookups.ResidentsAsync();
        ViewData["ServicePackageIdOptions"] = await lookups.PackagesAsync();
    }
}
