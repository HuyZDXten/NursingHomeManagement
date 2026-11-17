using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.DataAccessObjects;

/// <summary>MODULE VẬN HÀNH (Viên) — BL-V5: cấu hình cơ sở (UC-129) và danh mục dùng chung (UC-130).</summary>
public sealed class SystemDAO : GenericDAO<SystemSetting>
{
    public static SystemDAO Instance { get; } = new();

    private SystemDAO()
    {
    }

    // ---------------- Cấu hình ----------------

    public async Task<List<SystemSetting>> ListSettingsAsync()
    {
        await using var context = CreateContext();
        return await context.SystemSettings.AsNoTracking().ToListAsync();
    }

    /// <summary>Ghi cả nhóm khóa trong MỘT transaction (hoặc lưu hết hoặc không lưu gì).</summary>
    public async Task SaveSettingsAsync(IReadOnlyDictionary<string, string> values, int? updatedById, DateTime now)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var existing = await context.SystemSettings.Where(item => values.Keys.Contains(item.Key)).ToDictionaryAsync(item => item.Key);
        foreach (var (key, value) in values)
        {
            if (existing.TryGetValue(key, out var row))
            {
                row.Value = value;
                row.UpdatedAt = now;
                row.UpdatedById = updatedById;
            }
            else
            {
                context.SystemSettings.Add(new SystemSetting { Key = key, Value = value, UpdatedAt = now, UpdatedById = updatedById });
            }
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    /// <summary>Bỏ ghi đè → quay về giá trị trong appsettings. False nếu khóa chưa từng bị ghi đè.</summary>
    public async Task<bool> DeleteSettingAsync(string key)
    {
        await using var context = CreateContext();
        return await context.SystemSettings.Where(item => item.Key == key).ExecuteDeleteAsync() > 0;
    }

    // ---------------- Danh mục ----------------

    public async Task<List<Lookup>> ListLookupsAsync(string? category, bool includeInactive)
    {
        await using var context = CreateContext();
        var query = context.Lookups.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(item => item.Category == category);
        }

        if (!includeInactive)
        {
            query = query.Where(item => item.IsActive);
        }

        return await query.OrderBy(item => item.Category).ThenBy(item => item.SortOrder).ThenBy(item => item.Name).ToListAsync();
    }

    public async Task<Lookup?> GetLookupAsync(int id)
    {
        await using var context = CreateContext();
        return await context.Lookups.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task<Lookup> AddLookupAsync(Lookup lookup)
    {
        try
        {
            return await AddEntityAsync(lookup);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("Mã này đã có trong danh mục.");
        }
    }

    public async Task<Lookup?> UpdateLookupAsync(int id, Action<Lookup> apply)
    {
        try
        {
            await using var context = CreateContext();
            var row = await context.Lookups.FirstOrDefaultAsync(item => item.Id == id);
            if (row is null)
            {
                return null;
            }

            apply(row);
            await context.SaveChangesAsync();
            return row;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("Mã này đã có trong danh mục.");
        }
    }

    public async Task<bool> DeleteLookupAsync(int id)
    {
        await using var context = CreateContext();
        return await context.Lookups.Where(item => item.Id == id).ExecuteDeleteAsync() > 0;
    }
}
