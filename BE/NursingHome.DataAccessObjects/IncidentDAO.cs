using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.DataAccessObjects;

/// <summary>S3-H1: sự cố (UC-58, UC-59, UC-107).</summary>
public sealed class IncidentDAO : GenericDAO<Incident>
{
    public static IncidentDAO Instance { get; } = new();

    private IncidentDAO()
    {
    }

    /// <param name="onlyResidentIds">null = không giới hạn (Admin/Nurse/Doctor); có giá trị = phạm vi của Caregiver.</param>
    public async Task<List<Incident>> ListAsync(int? residentId, IReadOnlyCollection<int>? onlyResidentIds,
        IncidentStatus? status, IncidentSeverity? severity, DateTime? fromUtc, DateTime? toUtc)
    {
        await using var context = CreateContext();
        var query = context.Incidents.AsNoTracking()
            .Include(item => item.Resident)
            .Include(item => item.ReportedBy)
            .Include(item => item.ClosedBy)
            .AsQueryable();
        if (residentId.HasValue)
        {
            query = query.Where(item => item.ResidentId == residentId);
        }

        if (onlyResidentIds is not null)
        {
            query = query.Where(item => onlyResidentIds.Contains(item.ResidentId));
        }

        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status);
        }

        if (severity.HasValue)
        {
            query = query.Where(item => item.Severity == severity);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(item => item.OccurredAt >= fromUtc);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(item => item.OccurredAt < toUtc);
        }

        return await query.OrderByDescending(item => item.OccurredAt).ToListAsync();
    }

    public async Task<Incident?> GetDetailAsync(int id)
    {
        await using var context = CreateContext();
        return await context.Incidents.AsNoTracking()
            .Include(item => item.Resident)
            .Include(item => item.ReportedBy)
            .Include(item => item.ClosedBy)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    /// <summary>Sự cố đã đánh dấu chia sẻ của một cư dân (trang người thân).</summary>
    public async Task<List<Incident>> ListSharedForResidentAsync(int residentId, int take)
    {
        await using var context = CreateContext();
        return await context.Incidents.AsNoTracking()
            .Include(item => item.Resident)
            .Where(item => item.ResidentId == residentId && item.ShareWithFamily)
            .OrderByDescending(item => item.OccurredAt).Take(take).ToListAsync();
    }

    /// <summary>
    /// Cập nhật với điều kiện RowVersion client gửi lên: người khác đã cập nhật trước thì EF ném
    /// DbUpdateConcurrencyException (API trả 409). Sự cố đã đóng không được sửa.
    /// </summary>
    public async Task UpdateStatusAsync(int id, byte[] rowVersion, Action<Incident> apply)
    {
        await using var context = CreateContext();
        var incident = await context.Incidents.FindAsync(id) ?? throw new NotFoundException("Không tìm thấy sự cố.");
        if (incident.Status == IncidentStatus.Closed)
        {
            throw new BusinessRuleException("Sự cố đã đóng, không thể cập nhật.");
        }

        context.Entry(incident).Property(item => item.RowVersion).OriginalValue = rowVersion;
        apply(incident);
        await context.SaveChangesAsync();
    }

    /// <summary>Tài khoản Family đang hoạt động, được liên kết với cư dân và có quyền CanViewCare.</summary>
    public async Task<List<int>> FamilyCareViewerIdsAsync(int residentId)
    {
        await using var context = CreateContext();
        return await (from link in context.ResidentFamilyLinks
                      join account in context.Accounts on link.FamilyAccountId equals account.Id
                      where link.ResidentId == residentId && link.CanViewCare && account.IsActive && account.Role == Roles.Family
                      select account.Id).Distinct().ToListAsync();
    }
}
