using System.Text.Json;
using Microsoft.Extensions.Logging;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Admin;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>UC-13: nhật ký thao tác (chỉ thêm và đọc).</summary>
public class AuditService(IAuditRepository audits, FacilityClock clock, ILogger<AuditService> logger) : IAuditService
{
    public const int MaxPageSize = 100;

    private const int MaxDataLength = 4000;

    public async Task RecordAsync(CurrentUser? actor, string action, string entityType, string? entityId, string summary, object? data = null, string? ip = null)
    {
        try
        {
            await audits.AddAsync(new AuditLog
            {
                AccountId = actor?.AccountId,
                ActorName = Truncate(actor?.Name, 150),
                Action = Truncate(action, 60)!,
                EntityType = Truncate(entityType, 60)!,
                EntityId = Truncate(entityId, 60),
                Summary = Truncate(summary, 400)!,
                DataJson = Serialize(data),
                Ip = Truncate(ip, 45),
                At = clock.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không ghi được nhật ký thao tác {Action}", action);
        }
    }

    public async Task<PageDto<AuditLogDto>> ListAsync(AuditQuery query)
    {
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        DateTime? from = query.From is null ? null : clock.StartOfDayUtc(query.From.Value);
        DateTime? to = query.To is null ? null : clock.StartOfDayUtc(query.To.Value.AddDays(1));
        if (from is not null && to is not null && from >= to)
        {
            throw new BusinessRuleException("Ngày bắt đầu phải trước ngày kết thúc.");
        }

        var (items, total) = await audits.SearchAsync(from, to, query.AccountId, query.Action?.Trim(), query.EntityType?.Trim(),
            query.Search?.Trim(), (page - 1) * pageSize, pageSize);
        return new PageDto<AuditLogDto> { Items = items.Select(item => item.ToDto()).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    public Task<List<string>> ListActionsAsync() => audits.ListActionsAsync();

    private static string? Truncate(string? text, int max) =>
        string.IsNullOrEmpty(text) ? null : text.Length <= max ? text : text[..max];

    private static string? Serialize(object? data)
    {
        if (data is null)
        {
            return null;
        }

        string json = JsonSerializer.Serialize(data);
        return json.Length <= MaxDataLength ? json : json[..MaxDataLength];
    }
}

/// <summary>UC-129: cấu hình cơ sở. Chỉ các khóa trong <see cref="SettingsCatalog"/> sửa được; áp dụng ngay.</summary>
public class SettingsService(ISystemRepository system, FacilitySettingsStore store, FacilityClock clock, IAuditService audit) : ISettingsService
{
    public async Task<List<SettingDto>> ListAsync()
    {
        var overrides = (await system.ListSettingsAsync()).ToDictionary(item => item.Key);
        return Build(overrides);
    }

    public async Task<List<SettingDto>> SaveAsync(CurrentUser user, SaveSettingsRequest request)
    {
        var values = new Dictionary<string, string>();
        foreach (var item in request.Values)
        {
            var definition = SettingsCatalog.Find(item.Key) ?? throw new BusinessRuleException($"Khóa cấu hình không hợp lệ: {item.Key}.");
            if (values.ContainsKey(item.Key))
            {
                throw new BusinessRuleException($"Khóa {item.Key} bị gửi hai lần.");
            }

            values[item.Key] = SettingsCatalog.Normalize(definition, item.Value);
        }

        // Kiểm tra các cặp ngưỡng trên kết quả SAU khi áp toàn bộ thay đổi (đổi cả hai ngưỡng cùng lúc phải được).
        var candidate = SettingsCatalog.Clone(store.Value);
        var before = new Dictionary<string, string>();
        foreach (var (key, value) in values)
        {
            before[key] = SettingsCatalog.Read(candidate, key);
            SettingsCatalog.Write(candidate, key, value);
        }

        SettingsCatalog.ValidateConsistency(candidate);

        await system.SaveSettingsAsync(values, user.AccountId, clock.UtcNow);
        await ReloadAsync();

        var changed = values.Where(pair => before[pair.Key] != pair.Value).ToDictionary(pair => pair.Key, pair => new { Truoc = before[pair.Key], Sau = pair.Value });
        await audit.RecordAsync(user, "Settings.Update", "Settings", null,
            changed.Count == 0 ? "Lưu cấu hình cơ sở (không có thay đổi)" : $"Sửa cấu hình cơ sở: {string.Join(", ", changed.Keys.Select(Label))}", changed);
        return await ListAsync();
    }

    public async Task<List<SettingDto>> ResetAsync(CurrentUser user, string key)
    {
        _ = SettingsCatalog.Find(key) ?? throw new NotFoundException("Không có khóa cấu hình này.");
        if (!await system.DeleteSettingAsync(key))
        {
            throw new NotFoundException("Khóa này đang dùng giá trị mặc định, không có gì để bỏ ghi đè.");
        }

        await ReloadAsync();
        await audit.RecordAsync(user, "Settings.Reset", "Settings", key, $"Đặt lại cấu hình về mặc định: {Label(key)}");
        return await ListAsync();
    }

    public async Task ReloadAsync() => store.Apply(await system.ListSettingsAsync());

    private List<SettingDto> Build(IReadOnlyDictionary<string, SystemSetting> overrides)
    {
        var baseline = store.Baseline;
        var current = store.Value;
        return SettingsCatalog.All.Select(definition =>
        {
            overrides.TryGetValue(definition.Key, out var row);
            return new SettingDto
            {
                Key = definition.Key,
                Label = definition.Label,
                Group = definition.Group,
                Kind = definition.Kind.ToString(),
                Value = SettingsCatalog.Read(current, definition.Key),
                DefaultValue = SettingsCatalog.Read(baseline, definition.Key),
                IsOverridden = row is not null,
                Min = definition.Kind == SettingKind.Text ? null : definition.Min,
                Max = definition.Kind == SettingKind.Text ? null : definition.Max,
                UpdatedAt = row?.UpdatedAt
            };
        }).ToList();
    }

    private static string Label(string key) => SettingsCatalog.Find(key)?.Label ?? key;
}

/// <summary>UC-130: danh mục dùng chung. Tên danh mục được lưu thẳng vào bản ghi dùng nó, nên sửa/xóa danh mục không làm hỏng dữ liệu cũ.</summary>
public class LookupService(ISystemRepository system) : ILookupService
{
    public async Task<List<LookupDto>> ListAsync(string? category, bool includeInactive)
    {
        if (!string.IsNullOrWhiteSpace(category) && !LookupCategories.IsValid(category))
        {
            throw new BusinessRuleException("Loại danh mục chỉ nhận Relationship, Department hoặc Position.");
        }

        return (await system.ListLookupsAsync(category, includeInactive)).Select(item => item.ToDto()).ToList();
    }

    public async Task<LookupDto> CreateAsync(SaveLookupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Category) || !LookupCategories.IsValid(request.Category))
        {
            throw new BusinessRuleException("Chọn loại danh mục: Relationship, Department hoặc Position.");
        }

        var created = await system.AddLookupAsync(new Lookup
        {
            Category = request.Category,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        });
        return created.ToDto();
    }

    public async Task<LookupDto> UpdateAsync(int id, SaveLookupRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var existing = await system.GetLookupAsync(id) ?? throw new NotFoundException("Không tìm thấy mục danh mục.");
            if (existing.Category != request.Category)
            {
                throw new BusinessRuleException("Không đổi được loại danh mục của một mục đã tạo.");
            }
        }

        var updated = await system.UpdateLookupAsync(id, row =>
        {
            row.Code = request.Code.Trim();
            row.Name = request.Name.Trim();
            row.SortOrder = request.SortOrder;
            row.IsActive = request.IsActive;
        }) ?? throw new NotFoundException("Không tìm thấy mục danh mục.");
        return updated.ToDto();
    }

    public async Task DeleteAsync(int id)
    {
        if (!await system.DeleteLookupAsync(id))
        {
            throw new NotFoundException("Không tìm thấy mục danh mục.");
        }
    }
}
