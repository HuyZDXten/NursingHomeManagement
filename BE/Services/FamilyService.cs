using Microsoft.EntityFrameworkCore;
using NursingHomeManagement.Data;
using NursingHomeManagement.Models;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Services;

public sealed class FamilyService(
    ApplicationDbContext database,
    CurrentUser user,
    ResidentAccessService access)
{
    public async Task<List<Resident>> ListAsync()
    {
        await user.RequireRoleAsync(Roles.Family);
        return await database.ResidentFamilyLinks.AsNoTracking().Where(link => link.FamilyUserId == user.Id)
            .Select(link => link.Resident).OrderBy(resident => resident.FullName).ToListAsync();
    }

    public async Task<FamilyDetailsViewModel> DetailsAsync(int residentId, int page)
    {
        await user.RequireRoleAsync(Roles.Family);
        await access.RequireAsync(user, residentId, ResidentSection.Basic);
        page = Math.Clamp(page, 1, 100000);
        int skip = (page - 1) * 10;
        var link = await database.ResidentFamilyLinks.AsNoTracking().SingleAsync(item =>
            item.ResidentId == residentId && item.FamilyUserId == user.Id);
        return new FamilyDetailsViewModel
        {
            Resident = await database.Residents.AsNoTracking().SingleAsync(resident => resident.Id == residentId),
            Permissions = link,
            Page = page,
            Health = link.CanViewHealth
                ? await database.VitalSigns.AsNoTracking().Where(item => item.ResidentId == residentId && item.ShareWithFamily)
                    .OrderByDescending(item => item.RecordedAt).Skip(skip).Take(10).ToListAsync() : [],
            Care = link.CanViewCare
                ? await database.CareRecords.AsNoTracking().Include(item => item.CareTask)
                    .Where(item => item.CareTask.ResidentId == residentId && item.ShareWithFamily)
                    .OrderByDescending(item => item.PerformedAt).Skip(skip).Take(10).ToListAsync() : [],
            Medication = link.CanViewMedication
                ? await database.MedicationSchedules.AsNoTracking().Where(item => item.ResidentId == residentId)
                    .OrderByDescending(item => item.StartDate).Skip(skip).Take(10).ToListAsync() : [],
            Invoices = link.CanViewBilling
                ? await database.Invoices.AsNoTracking().Include(item => item.Items).Include(item => item.Payments)
                    .Where(item => item.ResidentId == residentId && item.Status != InvoiceStatus.Draft)
                    .OrderByDescending(item => item.BillingPeriod).Skip(skip).Take(10).ToListAsync() : []
        };
    }
}
