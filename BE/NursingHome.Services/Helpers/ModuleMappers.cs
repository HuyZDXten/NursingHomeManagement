using System.Text.Json;
using NursingHome.BusinessObjects.Entities;
using NursingHome.DTOs.Care;

namespace NursingHome.Services.Helpers;

/// <summary>Entity → DTO của module Vận hành &amp; Tài chính (Viên). Phần lõi vẫn ở DtoMapper.cs.</summary>
/// Mỗi task viết phần của mình ở file riêng OperationsMapper.&lt;Task&gt;.cs (partial) để không đụng nhau.
public static partial class OperationsMapper
{
    // Ví dụ: public static InquiryDto ToDto(this Inquiry inquiry) => new() { ... };  → OperationsMapper.Admissions.cs
}

/// <summary>Entity → DTO của module Chăm sóc &amp; Gia đình (Huy). Phần lõi vẫn ở DtoMapper.cs.</summary>
public static partial class CareMapper
{
    public static IncidentDto ToDto(this Incident item) => new()
    {
        Id = item.Id,
        ResidentId = item.ResidentId,
        ResidentName = item.Resident?.FullName ?? "",
        ResidentCode = item.Resident?.Code ?? "",
        Type = item.Type.ToString(),
        Severity = item.Severity.ToString(),
        OccurredAt = item.OccurredAt,
        Location = item.Location,
        Description = item.Description,
        ImmediateAction = item.ImmediateAction,
        ReportedByName = item.ReportedBy?.FullName ?? "",
        Status = item.Status.ToString(),
        Resolution = item.Resolution,
        ClosedByName = item.ClosedBy?.FullName,
        ClosedAt = item.ClosedAt,
        ShareWithFamily = item.ShareWithFamily,
        FamilyNotifiedAt = item.FamilyNotifiedAt,
        CreatedAt = item.CreatedAt,
        RowVersion = Convert.ToBase64String(item.RowVersion)
    };

    public static FamilyIncidentDto ToFamilyDto(this Incident item) => new()
    {
        Id = item.Id,
        Type = item.Type.ToString(),
        Severity = item.Severity.ToString(),
        OccurredAt = item.OccurredAt,
        Location = item.Location,
        Description = item.Description,
        ImmediateAction = item.ImmediateAction,
        Status = item.Status.ToString(),
        Resolution = item.Resolution,
        ClosedAt = item.ClosedAt
    };

    public static ConditionDto ToDto(this MedicalCondition item) => new()
    {
        Id = item.Id,
        ResidentId = item.ResidentId,
        Name = item.Name,
        DiagnosedOn = item.DiagnosedOn,
        Status = item.Status.ToString(),
        Notes = item.Notes,
        RecordedByName = item.RecordedBy?.FullName ?? "",
        CreatedAt = item.CreatedAt
    };

    public static AllergyDto ToDto(this Allergy item) => new()
    {
        Id = item.Id,
        ResidentId = item.ResidentId,
        Allergen = item.Allergen,
        Type = item.Type.ToString(),
        Reaction = item.Reaction,
        Severity = item.Severity.ToString(),
        RecordedByName = item.RecordedBy?.FullName ?? "",
        CreatedAt = item.CreatedAt
    };

    public static DietPlanDto ToDto(this DietPlan item) => new()
    {
        Id = item.Id,
        ResidentId = item.ResidentId,
        DietType = item.DietType.ToString(),
        Texture = item.Texture.ToString(),
        Restrictions = item.Restrictions,
        StartDate = item.StartDate,
        EndDate = item.EndDate,
        PrescribedByName = item.PrescribedBy?.FullName ?? "",
        CreatedAt = item.CreatedAt
    };

    public static VisitBookingDto ToDto(this VisitBooking item) => new()
    {
        Id = item.Id,
        ResidentId = item.ResidentId,
        ResidentName = item.Resident?.FullName ?? "",
        ResidentCode = item.Resident?.Code ?? "",
        FamilyAccountId = item.FamilyAccountId,
        FamilyName = item.FamilyAccount?.FullName ?? "",
        FamilyPhone = item.FamilyAccount?.Phone,
        VisitDate = item.VisitDate,
        SlotStart = item.SlotStart,
        SlotEnd = item.SlotEnd,
        GuestCount = item.GuestCount,
        GuestNames = item.GuestNames,
        Note = item.Note,
        Status = item.Status.ToString(),
        ReviewedByName = item.ReviewedBy?.FullName,
        ReviewNote = item.ReviewNote,
        CheckedInAt = item.CheckedInAt,
        CreatedAt = item.CreatedAt
    };

    public static CarePlanItemDto ToDto(this CarePlanItem item) => new()
    {
        Id = item.Id,
        Title = item.Title,
        Instructions = item.Instructions,
        TimeOfDay = item.TimeOfDay,
        DaysOfWeek = item.DaysOfWeek,
        DefaultAssigneeId = item.DefaultAssigneeId,
        DefaultAssigneeName = item.DefaultAssignee?.FullName
    };

    public static CarePlanDto ToDto(this CarePlan item) => new()
    {
        Id = item.Id,
        ResidentId = item.ResidentId,
        Title = item.Title,
        Goals = item.Goals,
        StartDate = item.StartDate,
        EndDate = item.EndDate,
        IsActive = item.IsActive,
        CreatedByName = item.CreatedBy?.FullName ?? "",
        CreatedAt = item.CreatedAt,
        Items = item.Items.OrderBy(i => i.TimeOfDay).Select(i => i.ToDto()).ToList()
    };

    public static DailyCareLogDto ToDto(this DailyCareLog item) => new()
    {
        Id = item.Id,
        ResidentId = item.ResidentId,
        LogDate = item.LogDate,
        Category = item.Category.ToString(),
        Level = item.Level.ToString(),
        Note = item.Note,
        RecordedByName = item.RecordedBy?.FullName ?? "",
        RecordedAt = item.RecordedAt,
        ShareWithFamily = item.ShareWithFamily
    };

    public static MealLogDto ToDto(this MealLog item) => new()
    {
        Id = item.Id,
        ResidentId = item.ResidentId,
        MealDate = item.MealDate,
        Meal = item.Meal.ToString(),
        IntakePercent = item.IntakePercent,
        FluidMl = item.FluidMl,
        Note = item.Note,
        RecordedByName = item.RecordedBy?.FullName ?? "",
        RecordedAt = item.RecordedAt,
        ShareWithFamily = item.ShareWithFamily
    };

    public static AssessmentDto ToDto(this Assessment item) => new()
    {
        Id = item.Id,
        ResidentId = item.ResidentId,
        Type = item.Type.ToString(),
        Score = item.Score,
        MaxScore = item.MaxScore,
        RiskLevel = item.RiskLevel.ToString(),
        Answers = string.IsNullOrWhiteSpace(item.AnswersJson)
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, string>>(item.AnswersJson) ?? [],
        Note = item.Note,
        AssessedByName = item.AssessedBy?.FullName ?? "",
        AssessedAt = item.AssessedAt,
        NextDueOn = item.NextDueOn
    };

    public static DoctorVisitDto ToDto(this DoctorVisit item) => new()
    {
        Id = item.Id,
        ResidentId = item.ResidentId,
        DoctorId = item.DoctorId,
        DoctorName = item.Doctor?.FullName ?? "",
        VisitedAt = item.VisitedAt,
        Subjective = item.Subjective,
        Objective = item.Objective,
        Assessment = item.Assessment,
        Plan = item.Plan,
        FollowUpOn = item.FollowUpOn,
        ShareWithFamily = item.ShareWithFamily,
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt
    };

    public static ThresholdDto ToDto(this VitalThreshold item) => new()
    {
        ResidentId = item.ResidentId,
        IsCustom = true,
        SystolicHigh = item.SystolicHigh,
        SystolicLow = item.SystolicLow,
        HeartRateHigh = item.HeartRateHigh,
        HeartRateLow = item.HeartRateLow,
        TemperatureHigh = item.TemperatureHigh,
        SpO2Low = item.SpO2Low,
        Note = item.Note,
        SetByName = item.SetBy?.FullName,
        UpdatedAt = item.UpdatedAt
    };

    public static FeedbackDto ToDto(this Feedback item) => new()
    {
        Id = item.Id,
        FamilyAccountId = item.FamilyAccountId,
        FamilyName = item.FamilyAccount?.FullName ?? "",
        ResidentId = item.ResidentId,
        ResidentName = item.Resident?.FullName,
        Category = item.Category.ToString(),
        Rating = item.Rating,
        Subject = item.Subject,
        Content = item.Content,
        Status = item.Status.ToString(),
        AssignedToId = item.AssignedToId,
        AssignedToName = item.AssignedTo?.FullName,
        Response = item.Response,
        RespondedByName = item.RespondedBy?.FullName,
        RespondedAt = item.RespondedAt,
        CreatedAt = item.CreatedAt
    };

    public static AiInsightDto ToDto(this AiInsight item) => new()
    {
        Id = item.Id,
        ResidentId = item.ResidentId,
        ResidentCode = item.Resident?.Code ?? "",
        ResidentName = item.Resident?.FullName ?? "",
        Type = item.Type.ToString(),
        ForDate = item.ForDate,
        Content = item.Content,
        SourceJson = item.SourceJson,
        Status = item.Status.ToString(),
        GeneratedAt = item.GeneratedAt,
        ReviewedByName = item.ReviewedBy?.FullName,
        ReviewedAt = item.ReviewedAt
    };
}
