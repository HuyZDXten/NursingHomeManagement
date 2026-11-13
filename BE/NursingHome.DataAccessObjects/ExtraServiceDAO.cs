using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.DataAccessObjects;

/// <summary>MODULE TÀI CHÍNH (Viên) — S3-V5: danh mục dịch vụ phát sinh (UC-85) và lần sử dụng (UC-86).</summary>
public sealed class ExtraServiceDAO : GenericDAO<ExtraService>
{
    public static ExtraServiceDAO Instance { get; } = new();

    private ExtraServiceDAO()
    {
    }

    // ---------------- Danh mục ----------------

    public async Task<List<ExtraService>> ListServicesAsync(bool activeOnly)
    {
        await using var context = CreateContext();
        return await context.ExtraServices.AsNoTracking()
            .Where(item => !activeOnly || item.IsActive)
            .OrderBy(item => item.Name)
            .ToListAsync();
    }

    public async Task<ExtraService?> GetServiceAsync(int id)
    {
        await using var context = CreateContext();
        return await context.ExtraServices.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task<bool> ServiceNameExistsAsync(string name, int? excludeId)
    {
        await using var context = CreateContext();
        return await context.ExtraServices.AnyAsync(item => item.Name == name && item.Id != excludeId);
    }

    public Task<ExtraService> AddServiceAsync(ExtraService service) => AddAsync(service);

    public Task UpdateServiceAsync(ExtraService service) => UpdateAsync(service);

    // ---------------- Lần sử dụng ----------------

    private static IQueryable<ServiceUsage> WithDetails(BusinessObjects.NursingHomeContext context) => context.ServiceUsages.AsNoTracking()
        .Include(item => item.Resident)
        .Include(item => item.ExtraService)
        .Include(item => item.RecordedBy)
        .Include(item => item.InvoiceItem);

    /// <param name="residentIds">NULL = mọi cư dân (đã lọc quyền ở Service).</param>
    public async Task<List<ServiceUsage>> ListUsagesAsync(IReadOnlyCollection<int>? residentIds, DateOnly from, DateOnly to, int take)
    {
        var ids = residentIds?.ToList();
        await using var context = CreateContext();
        return await WithDetails(context)
            .Where(item => item.UsedOn >= from && item.UsedOn <= to && (ids == null || ids.Contains(item.ResidentId)))
            .OrderByDescending(item => item.UsedOn).ThenByDescending(item => item.Id)
            .Take(take)
            .ToListAsync();
    }

    public async Task<ServiceUsage?> GetUsageAsync(int id)
    {
        await using var context = CreateContext();
        return await WithDetails(context).FirstOrDefaultAsync(item => item.Id == id);
    }

    public Task<ServiceUsage> AddUsageAsync(ServiceUsage usage) => AddEntityAsync(usage);

    /// <summary>Chỉ xóa khi chưa vào hóa đơn (DELETE có điều kiện — không đua với lúc tạo hóa đơn).</summary>
    public async Task DeleteUsageAsync(int id)
    {
        await using var context = CreateContext();
        var deleted = await context.ServiceUsages.Where(item => item.Id == id && item.InvoiceItemId == null).ExecuteDeleteAsync();
        if (deleted == 0)
        {
            throw await context.ServiceUsages.AnyAsync(item => item.Id == id)
                ? new BusinessRuleException("Lần sử dụng đã vào hóa đơn, không xóa được.")
                : new NotFoundException("Không tìm thấy lần sử dụng.");
        }
    }
}
