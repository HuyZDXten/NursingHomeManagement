using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;

namespace NursingHome.DataAccessObjects;

public sealed record TimelineEntry(string Kind, DateTime At, string Title, string Detail, string RecordedBy);

/// <summary>S3-H6 (UC-108): gộp các bản ghi đã chia sẻ trong ngày cho gia đình.</summary>
public sealed class FamilyTimelineDAO : GenericDAO<Feedback>
{
    public static FamilyTimelineDAO Instance { get; } = new();

    private FamilyTimelineDAO()
    {
    }

    public async Task<List<TimelineEntry>> ForDateAsync(
        int residentId,
        DateOnly date,
        DateTime startUtc,
        DateTime endUtc,
        bool includeCare,
        bool includeHealth)
    {
        await using var context = CreateContext();
        var items = new List<TimelineEntry>();

        if (includeCare)
        {
            var meals = await context.MealLogs.AsNoTracking()
                .Include(m => m.RecordedBy)
                .Where(m => m.ResidentId == residentId && m.MealDate == date && m.ShareWithFamily)
                .ToListAsync();
            foreach (var m in meals)
            {
                string mealLabel = m.Meal switch
                {
                    MealType.Breakfast => "Bữa sáng",
                    MealType.Lunch => "Bữa trưa",
                    MealType.Dinner => "Bữa tối",
                    _ => "Bữa phụ"
                };
                string fluid = m.FluidMl.HasValue ? $", uống {m.FluidMl} ml" : "";
                string note = string.IsNullOrWhiteSpace(m.Note) ? "" : $" ({m.Note})";
                items.Add(new TimelineEntry("Meal", m.RecordedAt, mealLabel,
                    $"{mealLabel}: ăn {m.IntakePercent}%{fluid}{note}", m.RecordedBy.FullName));
            }

            var adls = await context.DailyCareLogs.AsNoTracking()
                .Include(l => l.RecordedBy)
                .Where(l => l.ResidentId == residentId && l.LogDate == date && l.ShareWithFamily)
                .ToListAsync();
            foreach (var l in adls)
            {
                string cat = l.Category switch
                {
                    DailyCareCategory.Bathing => "Tắm rửa",
                    DailyCareCategory.Toileting => "Vệ sinh",
                    DailyCareCategory.Mobility => "Vận động",
                    DailyCareCategory.Sleep => "Giấc ngủ",
                    DailyCareCategory.Mood => "Tâm trạng",
                    _ => "Sinh hoạt khác"
                };
                string lvl = l.Level switch
                {
                    DailyCareLevel.Independent => "tự làm",
                    DailyCareLevel.NeedsHelp => "cần hỗ trợ",
                    DailyCareLevel.Dependent => "phụ thuộc hoàn toàn",
                    DailyCareLevel.Good => "tốt",
                    DailyCareLevel.Fair => "trung bình",
                    _ => "kém"
                };
                string note = string.IsNullOrWhiteSpace(l.Note) ? "" : $" — {l.Note}";
                items.Add(new TimelineEntry("Adl", l.RecordedAt, cat,
                    $"{cat}: {lvl}{note}", l.RecordedBy.FullName));
            }

            var careRecords = await context.CareRecords.AsNoTracking()
                .Include(r => r.CareTask)
                .Include(r => r.RecordedBy)
                .Where(r => r.CareTask.ResidentId == residentId && r.ShareWithFamily &&
                            r.PerformedAt >= startUtc && r.PerformedAt < endUtc)
                .ToListAsync();
            foreach (var r in careRecords)
            {
                items.Add(new TimelineEntry("Care", r.PerformedAt, r.CareTask.Title, r.Result, r.RecordedBy.FullName));
            }

            var incidents = await context.Incidents.AsNoTracking()
                .Include(i => i.ReportedBy)
                .Where(i => i.ResidentId == residentId && i.ShareWithFamily &&
                            i.Status == IncidentStatus.Closed &&
                            i.OccurredAt >= startUtc && i.OccurredAt < endUtc)
                .ToListAsync();
            foreach (var i in incidents)
            {
                string typeText = i.Type switch
                {
                    IncidentType.Fall => "Té ngã",
                    IncidentType.Injury => "Chấn thương",
                    IncidentType.Behavior => "Hành vi",
                    _ => "Sự cố khác"
                };
                items.Add(new TimelineEntry("Incident", i.OccurredAt, $"Sự cố: {typeText}",
                    $"{typeText} — {i.Resolution ?? i.Description}", i.ReportedBy.FullName));
            }
        }

        if (includeHealth)
        {
            var vitals = await context.VitalSigns.AsNoTracking()
                .Include(v => v.RecordedBy)
                .Where(v => v.ResidentId == residentId && v.ShareWithFamily &&
                            v.RecordedAt >= startUtc && v.RecordedAt < endUtc)
                .ToListAsync();
            foreach (var v in vitals)
            {
                string spo2 = v.SpO2.HasValue ? $", SpO2 {v.SpO2}%" : "";
                items.Add(new TimelineEntry("Vital", v.RecordedAt, "Đo sinh hiệu",
                    $"HA {v.Systolic}/{v.Diastolic}, mạch {v.HeartRate}, {v.Temperature}°C{spo2}", v.RecordedBy.FullName));
            }

            var visits = await context.DoctorVisits.AsNoTracking()
                .Include(v => v.Doctor)
                .Where(v => v.ResidentId == residentId && v.ShareWithFamily &&
                            v.VisitedAt >= startUtc && v.VisitedAt < endUtc)
                .ToListAsync();
            foreach (var v in visits)
            {
                string detail = string.IsNullOrWhiteSpace(v.Plan) ? v.Assessment : $"{v.Assessment} · Kế hoạch: {v.Plan}";
                items.Add(new TimelineEntry("DoctorVisit", v.VisitedAt, "Bác sĩ thăm khám", detail, v.Doctor.FullName));
            }
        }

        return items.OrderBy(i => i.At).ToList();
    }
}

/// <summary>S3-H6 (UC-109, UC-110): phản hồi / khiếu nại gia đình.</summary>
public sealed class FeedbackDAO : GenericDAO<Feedback>
{
    public static FeedbackDAO Instance { get; } = new();

    private FeedbackDAO()
    {
    }

    public async Task<List<Feedback>> ListAsync(int? familyAccountId, FeedbackStatus? status, FeedbackCategory? category)
    {
        await using var context = CreateContext();
        var query = context.Feedbacks.AsNoTracking()
            .Include(f => f.FamilyAccount)
            .Include(f => f.Resident)
            .Include(f => f.AssignedTo)
            .Include(f => f.RespondedBy)
            .AsQueryable();

        if (familyAccountId.HasValue)
        {
            query = query.Where(f => f.FamilyAccountId == familyAccountId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(f => f.Status == status.Value);
        }

        if (category.HasValue)
        {
            query = query.Where(f => f.Category == category.Value);
        }

        return await query.OrderByDescending(f => f.CreatedAt).ToListAsync();
    }

    public async Task<Feedback?> GetDetailAsync(int id)
    {
        await using var context = CreateContext();
        return await context.Feedbacks.AsNoTracking()
            .Include(f => f.FamilyAccount)
            .Include(f => f.Resident)
            .Include(f => f.AssignedTo)
            .Include(f => f.RespondedBy)
            .FirstOrDefaultAsync(f => f.Id == id);
    }
}
