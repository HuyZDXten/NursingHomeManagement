using System.Data;
using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.DataAccessObjects;

public sealed class ResidentDAO : GenericDAO<Resident>
{
    public static ResidentDAO Instance { get; } = new();

    private ResidentDAO()
    {
    }

    /// <summary>Kèm giường đang ở (BedAssignment chưa kết thúc → Bed → Room).</summary>
    private static IQueryable<Resident> WithCurrentBed(NursingHomeContext context) =>
        context.Residents.AsNoTracking()
            .Include(resident => resident.BedAssignments.Where(assignment => assignment.EndedAt == null))
            .ThenInclude(assignment => assignment.Bed)
            .ThenInclude(bed => bed.Room);

    /// <param name="onlyIds">Null = tất cả; có giá trị = chỉ các cư dân này (Caregiver chỉ thấy người được giao).</param>
    public async Task<List<Resident>> ListAsync(IReadOnlyCollection<int>? onlyIds)
    {
        await using var context = CreateContext();
        var query = WithCurrentBed(context);
        if (onlyIds is not null)
        {
            query = query.Where(resident => onlyIds.Contains(resident.Id));
        }

        return await query.OrderBy(resident => resident.Code).ToListAsync();
    }

    public async Task<Resident?> GetDetailAsync(int id)
    {
        await using var context = CreateContext();
        return await WithCurrentBed(context).FirstOrDefaultAsync(resident => resident.Id == id);
    }

    public async Task<bool> CodeExistsAsync(string code, int? excludeId)
    {
        await using var context = CreateContext();
        return await context.Residents.AnyAsync(resident => resident.Code == code && resident.Id != excludeId);
    }

    public async Task<ResidentStatus?> GetStatusAsync(int id)
    {
        await using var context = CreateContext();
        return await context.Residents.Where(resident => resident.Id == id)
            .Select(resident => (ResidentStatus?)resident.Status).FirstOrDefaultAsync();
    }

    /// <summary>
    /// UC-32 Ngừng lưu trú, trong một transaction: trả giường, dừng lịch thuốc,
    /// hủy công việc còn mở (ghi CareRecord có lý do). Hồ sơ được giữ.
    /// </summary>
    public async Task DischargeAsync(int id, int byAccountId, DateTime now, string reason)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var resident = await context.Residents.FindAsync(id) ?? throw new NotFoundException("Không tìm thấy cư dân.");
        if (resident.Status != ResidentStatus.Active)
        {
            throw new BusinessRuleException("Cư dân đã ngừng lưu trú.");
        }

        var assignment = await context.BedAssignments.Include(item => item.Bed)
            .FirstOrDefaultAsync(item => item.ResidentId == id && item.EndedAt == null);
        if (assignment is not null)
        {
            assignment.EndedAt = now;
            assignment.Bed.Status = BedStatus.Available;
            assignment.Bed.UpdatedAt = now;
        }

        resident.Status = ResidentStatus.Discharged;
        resident.DischargedAt = now;
        resident.UpdatedAt = now;

        await context.MedicationSchedules.Where(item => item.ResidentId == id && item.IsActive)
            .ForEachAsync(schedule =>
            {
                schedule.IsActive = false;
                schedule.UpdatedAt = now;
            });

        var openTasks = await context.CareTasks.Where(task => task.ResidentId == id &&
            (task.Status == CareTaskStatus.Pending || task.Status == CareTaskStatus.InProgress)).ToListAsync();
        foreach (var task in openTasks)
        {
            task.Status = CareTaskStatus.Cancelled;
            task.UpdatedAt = now;
            context.CareRecords.Add(new CareRecord
            {
                CareTaskId = task.Id,
                Status = CareTaskStatus.Cancelled,
                Result = reason,
                RecordedById = byAccountId,
                PerformedAt = now
            });
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    // ---------------- Người liên hệ (UC-27) ----------------

    public async Task<List<FamilyContact>> ListContactsAsync(int residentId)
    {
        await using var context = CreateContext();
        return await context.FamilyContacts.AsNoTracking().Where(contact => contact.ResidentId == residentId)
            .OrderByDescending(contact => contact.IsEmergencyContact).ThenBy(contact => contact.FullName).ToListAsync();
    }

    public async Task<FamilyContact?> GetContactAsync(int id)
    {
        await using var context = CreateContext();
        return await context.FamilyContacts.AsNoTracking().FirstOrDefaultAsync(contact => contact.Id == id);
    }

    public Task<FamilyContact> AddContactAsync(FamilyContact contact) => AddEntityAsync(contact);

    public Task UpdateContactAsync(FamilyContact contact) => UpdateEntityAsync(contact);

    public Task DeleteContactAsync(FamilyContact contact) => DeleteEntityAsync(contact);

    // ---------------- Liên kết tài khoản Family (UC-28) ----------------

    public async Task<List<ResidentFamilyLink>> ListLinksAsync(int residentId)
    {
        await using var context = CreateContext();
        return await context.ResidentFamilyLinks.AsNoTracking().Include(link => link.FamilyAccount)
            .Where(link => link.ResidentId == residentId).ToListAsync();
    }

    public async Task<ResidentFamilyLink?> GetLinkAsync(int residentId, int familyAccountId)
    {
        await using var context = CreateContext();
        return await context.ResidentFamilyLinks.AsNoTracking()
            .FirstOrDefaultAsync(link => link.ResidentId == residentId && link.FamilyAccountId == familyAccountId);
    }

    public async Task<ResidentFamilyLink?> GetLinkByIdAsync(int id)
    {
        await using var context = CreateContext();
        return await context.ResidentFamilyLinks.AsNoTracking().FirstOrDefaultAsync(link => link.Id == id);
    }

    public async Task<List<ResidentFamilyLink>> ListLinksForFamilyAsync(int familyAccountId)
    {
        await using var context = CreateContext();
        return await context.ResidentFamilyLinks.AsNoTracking().Include(link => link.Resident)
            .Where(link => link.FamilyAccountId == familyAccountId)
            .OrderBy(link => link.Resident.FullName).ToListAsync();
    }

    /// <summary>Family đang liên kết với cư dân (dùng để gửi thông báo).</summary>
    public async Task<List<int>> FamilyAccountIdsAsync(int residentId, bool requireBilling)
    {
        await using var context = CreateContext();
        return await context.ResidentFamilyLinks
            .Where(link => link.ResidentId == residentId && (!requireBilling || link.CanViewBilling) && link.FamilyAccount.IsActive)
            .Select(link => link.FamilyAccountId).ToListAsync();
    }

    /// <summary>S3-V5: email các tài khoản Family đang hoạt động của cư dân (gửi hóa đơn, nhắc nợ).</summary>
    public async Task<List<string>> FamilyEmailsAsync(int residentId, bool requireBilling)
    {
        await using var context = CreateContext();
        return await context.ResidentFamilyLinks
            .Where(link => link.ResidentId == residentId && (!requireBilling || link.CanViewBilling) && link.FamilyAccount.IsActive)
            .Select(link => link.FamilyAccount.Email).Distinct().ToListAsync();
    }

    public Task<ResidentFamilyLink> AddLinkAsync(ResidentFamilyLink link) => AddEntityAsync(link);

    public Task UpdateLinkAsync(ResidentFamilyLink link) => UpdateEntityAsync(link);

    public Task DeleteLinkAsync(ResidentFamilyLink link) => DeleteEntityAsync(link);
}
