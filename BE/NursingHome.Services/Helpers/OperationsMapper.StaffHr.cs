using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.DTOs.Staffing;

namespace NursingHome.Services.Helpers;

public static partial class OperationsMapper
{
    public static StaffCertificateDto ToDto(this StaffCertificate certificate, DateOnly today) => new()
    {
        Id = certificate.Id,
        AccountId = certificate.AccountId,
        AccountName = certificate.Account?.FullName ?? "",
        Name = certificate.Name,
        Number = certificate.Number,
        IssuedBy = certificate.IssuedBy,
        IssuedOn = certificate.IssuedOn,
        ExpiresOn = certificate.ExpiresOn,
        State = StaffHrRules.StateOf(certificate.ExpiresOn, today).ToString(),
        DaysLeft = StaffHrRules.DaysLeft(certificate.ExpiresOn, today)
    };

    /// <summary>Ca của tôi: trạng thái chấm công theo giờ hiện tại.</summary>
    public static MyShiftDto ToMyShift(this AssignmentFact fact, DateTime now)
    {
        var (start, end) = StaffingRules.ShiftWindow(fact.WorkDate, fact.Shift);
        var attendance = fact.Attendance;
        string status = attendance switch
        {
            { CheckOutAt: not null } => "Done",
            not null => "InProgress",
            _ when now < start.AddMinutes(-StaffHrRules.CheckInEarlyMinutes) => "Upcoming",
            _ when now <= end => "Open",
            _ => "Missed"
        };
        return new MyShiftDto
        {
            ShiftAssignmentId = fact.AssignmentId,
            WorkDate = fact.WorkDate,
            ShiftCode = fact.Shift.Code,
            ShiftName = fact.Shift.Name,
            StartsAt = start,
            EndsAt = end,
            CheckInAt = attendance?.CheckInAt,
            CheckOutAt = attendance?.CheckOutAt,
            LateMinutes = attendance?.LateMinutes ?? 0,
            Status = status,
            CanCheckIn = attendance is null && StaffHrRules.CanCheckIn(start, end, now),
            CanCheckOut = attendance is { CheckOutAt: null }
        };
    }

    /// <summary>Dòng chấm công cho Admin; ca đã kết thúc mà không có công = Absent.</summary>
    public static AttendanceRecordDto ToRecord(this AssignmentFact fact, DateTime now, string accountName)
    {
        var (start, end) = StaffingRules.ShiftWindow(fact.WorkDate, fact.Shift);
        var attendance = fact.Attendance;
        string status = attendance is null
            ? (end <= now ? "Absent" : "Upcoming")
            : attendance.CheckOutAt is null && now <= end ? "InProgress"
            : attendance.LateMinutes > 0 ? "Late" : "OnTime";
        return new AttendanceRecordDto
        {
            ShiftAssignmentId = fact.AssignmentId,
            AccountId = fact.AccountId,
            AccountName = accountName,
            WorkDate = fact.WorkDate,
            ShiftName = fact.Shift.Name,
            StartsAt = start,
            EndsAt = end,
            CheckInAt = attendance?.CheckInAt,
            CheckOutAt = attendance?.CheckOutAt,
            LateMinutes = attendance?.LateMinutes ?? 0,
            Status = status,
            EditReason = attendance?.EditReason
        };
    }
}
