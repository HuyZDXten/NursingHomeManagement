using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;

namespace NursingHome.DataAccessObjects;

/// <summary>S4-H2 (AI-03, AI-06): quản lý bản nháp nhật ký AI và xu hướng sinh hiệu.</summary>
public sealed class AiInsightDAO : GenericDAO<AiInsight>
{
    public static AiInsightDAO Instance { get; } = new();

    private AiInsightDAO()
    {
    }

    public async Task<bool> TryAddAsync(AiInsight insight)
    {
        await using var context = CreateContext();
        bool exists = await context.AiInsights.AnyAsync(i =>
            i.ResidentId == insight.ResidentId && i.Type == insight.Type && i.ForDate == insight.ForDate);
        if (exists)
        {
            return false;
        }

        context.AiInsights.Add(insight);
        try
        {
            await context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    public async Task<bool> HasAnyForDateAsync(InsightType type, DateOnly forDate)
    {
        await using var context = CreateContext();
        return await context.AiInsights.AnyAsync(i => i.Type == type && i.ForDate == forDate);
    }

    public async Task<List<AiInsight>> ListAsync(DateOnly? date, InsightType? type, InsightStatus? status)
    {
        await using var context = CreateContext();
        var query = context.AiInsights.AsNoTracking()
            .Include(i => i.Resident)
            .Include(i => i.ReviewedBy)
            .AsQueryable();

        if (date.HasValue)
        {
            query = query.Where(i => i.ForDate == date.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(i => i.Type == type.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        return await query.OrderByDescending(i => i.ForDate).ThenBy(i => i.Resident.FullName).ToListAsync();
    }

    public async Task<AiInsight?> GetDetailAsync(long id)
    {
        await using var context = CreateContext();
        return await context.AiInsights.AsNoTracking()
            .Include(i => i.Resident)
            .Include(i => i.ReviewedBy)
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<List<AiInsight>> ListApprovedDigestsAsync(int residentId, DateOnly? date)
    {
        await using var context = CreateContext();
        var query = context.AiInsights.AsNoTracking()
            .Include(i => i.Resident)
            .Include(i => i.ReviewedBy)
            .Where(i => i.ResidentId == residentId &&
                        i.Type == InsightType.DailyDigest &&
                        i.Status == InsightStatus.Approved);
        if (date.HasValue)
        {
            query = query.Where(i => i.ForDate == date.Value);
        }

        return await query.OrderByDescending(i => i.ForDate).Take(30).ToListAsync();
    }

    public async Task<List<AiInsight>> ListVitalTrendsAsync(int residentId, int take)
    {
        await using var context = CreateContext();
        return await context.AiInsights.AsNoTracking()
            .Include(i => i.Resident)
            .Include(i => i.ReviewedBy)
            .Where(i => i.ResidentId == residentId && i.Type == InsightType.VitalTrend)
            .OrderByDescending(i => i.ForDate)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<(Resident Resident, bool IncludeCare, bool IncludeHealth)>> ActiveResidentsWithFamilyAsync()
    {
        await using var context = CreateContext();
        var residents = await context.Residents.AsNoTracking()
            .Where(r => r.Status == ResidentStatus.Active &&
                        context.ResidentFamilyLinks.Any(l => l.ResidentId == r.Id))
            .OrderBy(r => r.Id)
            .ToListAsync();

        var links = await context.ResidentFamilyLinks.AsNoTracking().ToListAsync();
        return residents.Select(r =>
        {
            var rLinks = links.Where(l => l.ResidentId == r.Id).ToList();
            return (r, rLinks.Any(l => l.CanViewCare), rLinks.Any(l => l.CanViewHealth));
        }).ToList();
    }

    public async Task<List<Resident>> ActiveResidentsAsync()
    {
        await using var context = CreateContext();
        return await context.Residents.AsNoTracking()
            .Where(r => r.Status == ResidentStatus.Active)
            .OrderBy(r => r.Id)
            .ToListAsync();
    }

    public async Task<List<VitalSign>> Last14DaysVitalsAsync(int residentId, DateTime sinceUtc)
    {
        await using var context = CreateContext();
        return await context.VitalSigns.AsNoTracking()
            .Where(v => v.ResidentId == residentId && v.RecordedAt >= sinceUtc)
            .OrderBy(v => v.RecordedAt)
            .ToListAsync();
    }
}
