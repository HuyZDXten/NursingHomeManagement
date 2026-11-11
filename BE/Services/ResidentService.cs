using System.Data;
using Microsoft.EntityFrameworkCore;
using NursingHomeManagement.Data;
using NursingHomeManagement.Models;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Services;

public sealed class ResidentService(
    ApplicationDbContext database,
    CurrentUser user,
    ResidentAccessService access,
    FacilityClock clock)
{
    public async Task<PagedList<Resident>> ListAsync(string? search, ResidentStatus? status, int page)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist, Roles.Caregiver);
        var query = database.Residents.AsNoTracking();
        if (user.IsInRole(Roles.Caregiver))
        {
            query = query.Where(resident => database.CareTasks.Any(task =>
                task.ResidentId == resident.Id && task.AssignedToId == user.Id &&
                (task.Status == CareTaskStatus.Pending || task.Status == CareTaskStatus.InProgress)));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(resident => resident.FullName.Contains(search) || resident.Code.Contains(search));
        }
        if (status.HasValue)
        {
            query = query.Where(resident => resident.Status == status);
        }
        return await PagedList<Resident>.CreateAsync(query.OrderBy(resident => resident.Code), page);
    }

    public async Task<Resident> GetAsync(int id)
    {
        await access.RequireAsync(user, id, ResidentSection.Basic);
        return await database.Residents.AsNoTracking().SingleAsync(resident => resident.Id == id);
    }

    public async Task<int> SaveAsync(int? id, ResidentForm form)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        if (form.DateOfBirth > clock.Today || form.DateOfBirth.Year < 1850)
        {
            throw new BusinessException("Ngày sinh không hợp lệ.");
        }
        if (await database.Residents.AnyAsync(resident => resident.Code == form.Code.Trim() && resident.Id != id))
        {
            throw new BusinessException("Mã cư dân đã tồn tại.");
        }
        var resident = id.HasValue
            ? await database.Residents.FindAsync(id.Value) ?? throw new KeyNotFoundException()
            : new Resident();
        resident.Code = form.Code.Trim();
        resident.FullName = form.FullName.Trim();
        resident.DateOfBirth = form.DateOfBirth;
        resident.Gender = form.Gender.Trim();
        resident.Notes = form.Notes?.Trim();
        if (!id.HasValue)
        {
            database.Residents.Add(resident);
        }
        await database.SaveChangesAsync();
        return resident.Id;
    }

    public async Task DischargeAsync(int id)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var resident = await database.Residents.FindAsync(id) ?? throw new KeyNotFoundException();
        if (resident.Status != ResidentStatus.Active)
        {
            throw new BusinessException("Cư dân đã ngừng lưu trú.");
        }
        var assignment = await database.BedAssignments.Include(item => item.Bed)
            .SingleOrDefaultAsync(item => item.ResidentId == id && item.EndedAt == null);
        if (assignment is not null)
        {
            assignment.EndedAt = DateTimeOffset.UtcNow;
            assignment.Bed.Status = BedStatus.Available;
        }
        resident.Status = ResidentStatus.Discharged;
        resident.DischargedAt = DateTimeOffset.UtcNow;
        var schedules = await database.MedicationSchedules.Where(item => item.ResidentId == id && item.IsActive).ToListAsync();
        foreach (var schedule in schedules)
        {
            schedule.IsActive = false;
        }
        var tasks = await database.CareTasks.Where(task => task.ResidentId == id &&
            (task.Status == CareTaskStatus.Pending || task.Status == CareTaskStatus.InProgress)).ToListAsync();
        foreach (var task in tasks)
        {
            task.Status = CareTaskStatus.Cancelled;
            database.CareRecords.Add(new CareRecord
            {
                CareTaskId = task.Id,
                Status = CareTaskStatus.Cancelled,
                Result = "Ngừng lưu trú.",
                RecordedById = user.Id
            });
        }
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<List<FamilyContact>> ContactsAsync(int residentId)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        await access.RequireAsync(user, residentId, ResidentSection.Basic);
        return await database.FamilyContacts.AsNoTracking().Where(contact => contact.ResidentId == residentId).ToListAsync();
    }

    public async Task SaveContactAsync(int? id, ContactForm form)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        await access.RequireAsync(user, form.ResidentId, ResidentSection.Basic);
        var contact = id.HasValue
            ? await database.FamilyContacts.SingleOrDefaultAsync(item => item.Id == id && item.ResidentId == form.ResidentId)
                ?? throw new KeyNotFoundException()
            : new FamilyContact { ResidentId = form.ResidentId };
        contact.FullName = form.FullName.Trim();
        contact.Relationship = form.Relationship.Trim();
        contact.Phone = form.Phone.Trim();
        contact.Email = form.Email;
        contact.IsEmergencyContact = form.IsEmergencyContact;
        if (!id.HasValue)
        {
            database.FamilyContacts.Add(contact);
        }
        await database.SaveChangesAsync();
    }

    public async Task<List<ResidentFamilyLink>> LinksAsync(int residentId)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        await access.RequireAsync(user, residentId, ResidentSection.Basic);
        return await database.ResidentFamilyLinks.AsNoTracking().Include(link => link.FamilyUser)
            .Where(link => link.ResidentId == residentId).ToListAsync();
    }

    public async Task LinkAsync(FamilyLinkForm form)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        await access.RequireAsync(user, form.ResidentId, ResidentSection.Basic);
        bool isFamily = await (from account in database.Users
                               join membership in database.UserRoles on account.Id equals membership.UserId
                               join role in database.Roles on membership.RoleId equals role.Id
                               where account.Id == form.FamilyUserId && account.IsActive && role.Name == Roles.Family
                               select account.Id).AnyAsync();
        if (!isFamily)
        {
            throw new BusinessException("Vui lòng chọn tài khoản Family đang hoạt động.");
        }
        var link = await database.ResidentFamilyLinks.SingleOrDefaultAsync(item =>
            item.ResidentId == form.ResidentId && item.FamilyUserId == form.FamilyUserId);
        if (link is null)
        {
            link = new ResidentFamilyLink { ResidentId = form.ResidentId, FamilyUserId = form.FamilyUserId };
            database.ResidentFamilyLinks.Add(link);
        }
        link.CanViewHealth = form.CanViewHealth;
        link.CanViewCare = form.CanViewCare;
        link.CanViewMedication = form.CanViewMedication;
        link.CanViewBilling = form.CanViewBilling;
        await database.SaveChangesAsync();
    }

    public async Task UnlinkAsync(int id)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Receptionist);
        var link = await database.ResidentFamilyLinks.FindAsync(id) ?? throw new KeyNotFoundException();
        database.ResidentFamilyLinks.Remove(link);
        await database.SaveChangesAsync();
    }
}
