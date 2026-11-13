using NursingHome.BusinessObjects.Entities;
using NursingHome.DTOs.Staffing;

namespace NursingHome.Services.Helpers;

/// <summary>S3-V2 ca trực &amp; phân công phụ trách.</summary>
public static partial class OperationsMapper
{
    public static ShiftDto ToDto(this Shift shift) => new()
    {
        Id = shift.Id,
        Code = shift.Code,
        Name = shift.Name,
        StartTime = shift.StartTime,
        EndTime = shift.EndTime,
        CrossesMidnight = shift.CrossesMidnight
    };

    /// <summary>Cần Include Account, Shift.</summary>
    public static ShiftAssignmentDto ToDto(this ShiftAssignment assignment, string? warning = null)
    {
        var (startUtc, endUtc) = StaffingRules.ShiftWindow(assignment.WorkDate, assignment.Shift);
        return new ShiftAssignmentDto
        {
            Id = assignment.Id,
            AccountId = assignment.AccountId,
            AccountName = assignment.Account.FullName,
            Role = assignment.Account.Role,
            ShiftId = assignment.ShiftId,
            ShiftCode = assignment.Shift.Code,
            ShiftName = assignment.Shift.Name,
            WorkDate = assignment.WorkDate,
            StartsAt = startUtc,
            EndsAt = endUtc,
            Note = assignment.Note,
            Warning = warning
        };
    }

    /// <summary>Cần Include Resident, Account, AssignedBy, Shift.</summary>
    public static CareAssignmentDto ToDto(this ResidentCareAssignment assignment) => new()
    {
        Id = assignment.Id,
        ResidentId = assignment.ResidentId,
        ResidentCode = assignment.Resident.Code,
        ResidentName = assignment.Resident.FullName,
        AccountId = assignment.AccountId,
        AccountName = assignment.Account.FullName,
        Role = assignment.Account.Role,
        ShiftId = assignment.ShiftId,
        ShiftName = assignment.Shift?.Name,
        StartDate = assignment.StartDate,
        EndDate = assignment.EndDate,
        IsActive = assignment.EndDate is null,
        AssignedByName = assignment.AssignedBy.FullName
    };
}
