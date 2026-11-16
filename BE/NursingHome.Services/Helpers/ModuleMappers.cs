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
}
