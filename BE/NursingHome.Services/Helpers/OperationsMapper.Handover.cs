using System.Text.Json;
using NursingHome.BusinessObjects.Entities;
using NursingHome.DTOs.Staffing;

namespace NursingHome.Services.Helpers;

/// <summary>S3-V4 nghỉ phép/đổi ca &amp; bàn giao ca.</summary>
public static partial class OperationsMapper
{
    private static readonly JsonSerializerOptions HighlightJson = new(JsonSerializerDefaults.Web);

    /// <summary>Cần Include Shift.</summary>
    public static LeaveShiftDto ToLeaveShift(this ShiftAssignment assignment) => new()
    {
        AssignmentId = assignment.Id,
        WorkDate = assignment.WorkDate,
        ShiftName = assignment.Shift.Name,
        ShiftCode = assignment.Shift.Code
    };

    /// <summary>Cần Include Account, SwapWithAccount, ReviewedBy, ShiftAssignment.Shift, SwapShiftAssignment.Shift.</summary>
    public static LeaveRequestDto ToDto(this LeaveRequest request) => new()
    {
        Id = request.Id,
        AccountId = request.AccountId,
        AccountName = request.Account.FullName,
        Role = request.Account.Role,
        Type = request.Type.ToString(),
        FromDate = request.FromDate,
        ToDate = request.ToDate,
        MyShift = request.ShiftAssignment?.ToLeaveShift(),
        SwapWithAccountId = request.SwapWithAccountId,
        SwapWithName = request.SwapWithAccount?.FullName,
        TheirShift = request.SwapShiftAssignment?.ToLeaveShift(),
        Reason = request.Reason,
        Status = request.Status.ToString(),
        ReviewedByName = request.ReviewedBy?.FullName,
        ReviewedAt = request.ReviewedAt,
        ReviewNote = request.ReviewNote,
        CreatedAt = request.CreatedAt
    };

    /// <summary>Cần Include Shift, FromAccount, AcknowledgedBy.</summary>
    public static HandoverDto ToDto(this ShiftHandover handover, bool canAcknowledge)
    {
        var (startUtc, endUtc) = StaffingRules.ShiftWindow(handover.WorkDate, handover.Shift);
        return new HandoverDto
        {
            Id = handover.Id,
            ShiftId = handover.ShiftId,
            ShiftCode = handover.Shift.Code,
            ShiftName = handover.Shift.Name,
            WorkDate = handover.WorkDate,
            StartsAt = startUtc,
            EndsAt = endUtc,
            FromAccountId = handover.FromAccountId,
            FromAccountName = handover.FromAccount.FullName,
            Role = handover.FromAccount.Role,
            Summary = handover.Summary,
            Highlights = ReadHighlights(handover.HighlightsJson),
            AiGenerated = handover.AiGenerated,
            AcknowledgedByName = handover.AcknowledgedBy?.FullName,
            AcknowledgedAt = handover.AcknowledgedAt,
            CreatedAt = handover.CreatedAt,
            CanAcknowledge = canAcknowledge
        };
    }

    public static string? WriteHighlights(IReadOnlyCollection<HandoverItemDto> items) =>
        items.Count == 0 ? null : JsonSerializer.Serialize(items, HighlightJson);

    public static List<HandoverItemDto> ReadHighlights(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<HandoverItemDto>>(json, HighlightJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
