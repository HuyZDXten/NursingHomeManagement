using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;

namespace NursingHome.DataAccessObjects;

/// <summary>S3-H5 (UC-64, UC-67, UC-68): ngưỡng sinh hiệu riêng, đánh giá định kỳ, phiếu khám bác sĩ.</summary>
public sealed class ClinicalDAO : GenericDAO<Assessment>
{
    public static ClinicalDAO Instance { get; } = new();

    private ClinicalDAO()
    {
    }

    public async Task<VitalThreshold?> GetThresholdAsync(int residentId)
    {
        await using var context = CreateContext();
        return await context.VitalThresholds.AsNoTracking()
            .Include(t => t.SetBy)
            .FirstOrDefaultAsync(t => t.ResidentId == residentId);
    }

    public async Task<VitalThreshold> UpsertThresholdAsync(VitalThreshold input)
    {
        await using var context = CreateContext();
        var existing = await context.VitalThresholds.FindAsync(input.ResidentId);
        if (existing is null)
        {
            context.VitalThresholds.Add(input);
        }
        else
        {
            existing.SystolicHigh = input.SystolicHigh;
            existing.SystolicLow = input.SystolicLow;
            existing.HeartRateHigh = input.HeartRateHigh;
            existing.HeartRateLow = input.HeartRateLow;
            existing.TemperatureHigh = input.TemperatureHigh;
            existing.SpO2Low = input.SpO2Low;
            existing.Note = input.Note;
            existing.SetById = input.SetById;
            existing.UpdatedAt = input.UpdatedAt;
        }

        await context.SaveChangesAsync();
        return (await GetThresholdAsync(input.ResidentId))!;
    }

    public async Task<List<Assessment>> ListAssessmentsAsync(int residentId)
    {
        await using var context = CreateContext();
        return await context.Assessments.AsNoTracking()
            .Include(a => a.AssessedBy)
            .Where(a => a.ResidentId == residentId)
            .OrderByDescending(a => a.AssessedAt)
            .ToListAsync();
    }

    public async Task<Assessment> AddAssessmentAsync(Assessment item)
    {
        var saved = await AddAsync(item);
        await using var context = CreateContext();
        return await context.Assessments.AsNoTracking()
            .Include(a => a.AssessedBy)
            .FirstAsync(a => a.Id == saved.Id);
    }

    public async Task<List<(Resident Resident, AssessmentType Type, Assessment? Latest)>> DueAssessmentsAsync(DateOnly today)
    {
        await using var context = CreateContext();
        var residents = await context.Residents.AsNoTracking()
            .Where(r => r.Status == ResidentStatus.Active)
            .OrderBy(r => r.FullName)
            .ToListAsync();

        var latestList = await context.Assessments.AsNoTracking()
            .GroupBy(a => new { a.ResidentId, a.Type })
            .Select(g => g.OrderByDescending(a => a.AssessedAt).First())
            .ToListAsync();

        var latestMap = latestList.ToDictionary(a => (a.ResidentId, a.Type));
        var due = new List<(Resident, AssessmentType, Assessment?)>();
        var types = Enum.GetValues<AssessmentType>();

        foreach (var r in residents)
        {
            foreach (var type in types)
            {
                latestMap.TryGetValue((r.Id, type), out var latest);
                if (latest is null || (latest.NextDueOn.HasValue && latest.NextDueOn.Value <= today))
                {
                    due.Add((r, type, latest));
                }
            }
        }

        return due;
    }

    public async Task<List<DoctorVisit>> ListDoctorVisitsAsync(int residentId)
    {
        await using var context = CreateContext();
        return await context.DoctorVisits.AsNoTracking()
            .Include(v => v.Doctor)
            .Where(v => v.ResidentId == residentId)
            .OrderByDescending(v => v.VisitedAt)
            .ToListAsync();
    }

    public async Task<DoctorVisit?> GetDoctorVisitAsync(int id)
    {
        await using var context = CreateContext();
        return await context.DoctorVisits.Include(v => v.Doctor)
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<DoctorVisit> AddDoctorVisitAsync(DoctorVisit visit)
    {
        var saved = await AddEntityAsync(visit);
        return (await GetDoctorVisitAsync(saved.Id))!;
    }

    public async Task<DoctorVisit> UpdateDoctorVisitAsync(DoctorVisit visit)
    {
        await UpdateEntityAsync(visit);
        return (await GetDoctorVisitAsync(visit.Id))!;
    }
}
