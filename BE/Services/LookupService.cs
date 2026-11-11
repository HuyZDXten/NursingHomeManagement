using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NursingHomeManagement.Data;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.Services;

public sealed class LookupService(ApplicationDbContext database, CurrentUser user)
{
    public async Task<List<SelectListItem>> ResidentsAsync()
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist, Roles.Caregiver);
        var query = database.Residents.Where(resident => resident.Status == ResidentStatus.Active);
        if (user.IsInRole(Roles.Caregiver))
        {
            query = query.Where(resident => database.CareTasks.Any(task => task.ResidentId == resident.Id &&
                task.AssignedToId == user.Id && (task.Status == CareTaskStatus.Pending || task.Status == CareTaskStatus.InProgress)));
        }
        return await query.OrderBy(resident => resident.FullName)
            .Select(resident => new SelectListItem(resident.Code + " · " + resident.FullName, resident.Id.ToString())).ToListAsync();
    }

    public async Task<List<SelectListItem>> UsersAsync(string roleName)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        return await (from account in database.Users
                      join membership in database.UserRoles on account.Id equals membership.UserId
                      join role in database.Roles on membership.RoleId equals role.Id
                      where account.IsActive && role.Name == roleName
                      orderby account.FullName
                      select new SelectListItem(account.FullName + " · " + account.Email, account.Id)).ToListAsync();
    }

    public async Task<List<SelectListItem>> BedsAsync()
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        return await database.Beds.Where(bed => bed.Status == BedStatus.Available && bed.Room.IsActive)
            .OrderBy(bed => bed.Room.Number).ThenBy(bed => bed.Number)
            .Select(bed => new SelectListItem(bed.Room.Number + " / " + bed.Number, bed.Id.ToString())).ToListAsync();
    }

    public async Task<List<SelectListItem>> RoomsAsync()
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        return await database.Rooms.OrderBy(room => room.Number)
            .Select(room => new SelectListItem(room.Number, room.Id.ToString())).ToListAsync();
    }

    public async Task<List<SelectListItem>> PackagesAsync()
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        return await database.ServicePackages.Where(package => package.IsActive).OrderBy(package => package.Name)
            .Select(package => new SelectListItem(package.Name, package.Id.ToString())).ToListAsync();
    }
}
