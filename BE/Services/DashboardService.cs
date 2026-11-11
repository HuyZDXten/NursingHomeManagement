using Microsoft.EntityFrameworkCore;
using NursingHomeManagement.Data;
using NursingHomeManagement.Models;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Services;

public sealed class DashboardService(
    ApplicationDbContext database,
    CurrentUser user,
    FacilityClock clock)
{
    public async Task<DashboardViewModel> GetAsync()
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        var start = clock.StartOfDay(clock.Today);
        var end = start.AddDays(1);
        var billed = await database.InvoiceItems.Where(item => item.Invoice.Status != InvoiceStatus.Draft &&
                item.Invoice.Status != InvoiceStatus.Cancelled)
            .SumAsync(item => (decimal?)Math.Round(item.Quantity * item.UnitPrice, 2)) ?? 0;
        var paid = await database.Payments.SumAsync(payment => (decimal?)payment.Amount) ?? 0;
        return new DashboardViewModel
        {
            Residents = await database.Residents.CountAsync(resident => resident.Status == ResidentStatus.Active),
            Rooms = await database.Rooms.CountAsync(),
            Beds = await database.Beds.CountAsync(),
            AvailableBeds = await database.Beds.CountAsync(bed => bed.Status == BedStatus.Available && bed.Room.IsActive),
            OccupiedBeds = await database.Beds.CountAsync(bed => bed.Status == BedStatus.Occupied),
            TasksToday = await database.CareTasks.CountAsync(task => task.DueAt >= start && task.DueAt < end),
            UnfinishedTasks = await database.CareTasks.CountAsync(task => task.Status == CareTaskStatus.Pending || task.Status == CareTaskStatus.InProgress),
            Paid = paid,
            Debt = billed - paid,
            Packages = await database.ServicePackages.AsNoTracking()
                .Where(package => package.IsActive)
                .OrderBy(package => package.MonthlyPrice)
                .ToListAsync()
        };
    }

    public async Task<DashboardViewModel> GetWelcomeAsync()
    {
        return new DashboardViewModel
        {
            Residents = await database.Residents.CountAsync(resident => resident.Status == ResidentStatus.Active),
            Rooms = await database.Rooms.CountAsync(room => room.IsActive),
            Beds = await database.Beds.CountAsync(bed => bed.Room.IsActive),
            AvailableBeds = await database.Beds.CountAsync(bed => bed.Status == BedStatus.Available && bed.Room.IsActive),
            OccupiedBeds = await database.Beds.CountAsync(bed => bed.Status == BedStatus.Occupied),
            Packages = await database.ServicePackages.AsNoTracking()
                .Where(package => package.IsActive)
                .OrderBy(package => package.MonthlyPrice)
                .ToListAsync()
        };
    }
}
