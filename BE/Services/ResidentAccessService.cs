using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NursingHomeManagement.Data;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.Services;

public enum ResidentSection
{
    Basic,
    Health,
    Care,
    Medication,
    Billing
}

public sealed class ResidentAccessService(ApplicationDbContext database)
{
    public async Task<bool> CanReadAsync(ClaimsPrincipal principal, int residentId, ResidentSection section, CancellationToken cancellationToken = default)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null || !await database.Users.AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken))
        {
            return false;
        }

        if (!await database.Residents.AnyAsync(resident => resident.Id == residentId, cancellationToken))
        {
            return false;
        }

        if (principal.IsInRole(Roles.Admin))
        {
            return true;
        }

        if (principal.IsInRole(Roles.Receptionist))
        {
            return section is ResidentSection.Basic or ResidentSection.Billing;
        }

        if (principal.IsInRole(Roles.Caregiver))
        {
            return section != ResidentSection.Billing && await database.CareTasks.AnyAsync(task => task.ResidentId == residentId && task.AssignedToId == userId && (task.Status == CareTaskStatus.Pending || task.Status == CareTaskStatus.InProgress), cancellationToken);
        }

        if (!principal.IsInRole(Roles.Family))
        {
            return false;
        }

        var link = await database.ResidentFamilyLinks.AsNoTracking().SingleOrDefaultAsync(link => link.ResidentId == residentId && link.FamilyUserId == userId, cancellationToken);
        return link is not null && section switch
        {
            ResidentSection.Basic => true,
            ResidentSection.Health => link.CanViewHealth,
            ResidentSection.Care => link.CanViewCare,
            ResidentSection.Medication => link.CanViewMedication,
            ResidentSection.Billing => link.CanViewBilling,
            _ => false
        };
    }

    public async Task RequireAsync(CurrentUser user, int residentId, ResidentSection section)
    {
        if (!await CanReadAsync(user.Principal, residentId, section))
        {
            throw new UnauthorizedAccessException();
        }
    }
}
