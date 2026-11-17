using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;

namespace NursingHome.DataAccessObjects;

/// <summary>MODULE VẬN HÀNH (Viên) — BL-V5, UC-13: nhật ký thao tác. Chỉ thêm và đọc, không có đường sửa/xóa.</summary>
public sealed class AuditDAO : GenericDAO<AuditLog>
{
    public static AuditDAO Instance { get; } = new();

    private AuditDAO()
    {
    }

    public async Task<(List<AuditLog> Items, int Total)> SearchAsync(
        DateTime? fromUtc, DateTime? toUtc, int? accountId, string? action, string? entityType, string? search, int skip, int take)
    {
        await using var context = CreateContext();
        var query = context.AuditLogs.AsNoTracking().AsQueryable();
        if (fromUtc is not null)
        {
            query = query.Where(item => item.At >= fromUtc);
        }

        if (toUtc is not null)
        {
            query = query.Where(item => item.At < toUtc);
        }

        if (accountId is not null)
        {
            query = query.Where(item => item.AccountId == accountId);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(item => item.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(item => item.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(item => item.Summary.Contains(search) || (item.ActorName != null && item.ActorName.Contains(search)));
        }

        int total = await query.CountAsync();
        var items = await query.OrderByDescending(item => item.At).ThenByDescending(item => item.Id).Skip(skip).Take(take).ToListAsync();
        return (items, total);
    }

    /// <summary>Các mã thao tác đã từng ghi, để lọc theo thao tác ở màn hình nhật ký.</summary>
    public async Task<List<string>> ListActionsAsync()
    {
        await using var context = CreateContext();
        return await context.AuditLogs.AsNoTracking().Select(item => item.Action).Distinct().OrderBy(item => item).ToListAsync();
    }
}
