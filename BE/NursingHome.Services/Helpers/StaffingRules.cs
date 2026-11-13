using NursingHome.BusinessObjects.Entities;

namespace NursingHome.Services.Helpers;

/// <summary>Quy tắc thuần của ca trực (S3-V2), dùng lại ở S3-V4 bàn giao ca và S4-V2 AI tóm tắt ca.</summary>
public static class StaffingRules
{
    /// <summary>
    /// Khung giờ thật (UTC) của một ca: bắt đầu = WorkDate + StartTime; ca qua đêm (EndTime &lt;= StartTime)
    /// kết thúc = WorkDate + 1 ngày + EndTime. Giờ trong danh mục ca là giờ cơ sở (UTC+7).
    /// </summary>
    public static (DateTime StartUtc, DateTime EndUtc) ShiftWindow(DateOnly workDate, Shift shift)
    {
        var endDate = shift.CrossesMidnight ? workDate.AddDays(1) : workDate;
        return (ToUtc(workDate, shift.StartTime), ToUtc(endDate, shift.EndTime));
    }

    /// <summary>Hai khung giờ chồng hoặc nối liền nhau (ca này hết thì ca kia bắt đầu ngay).</summary>
    public static bool Touches((DateTime StartUtc, DateTime EndUtc) a, (DateTime StartUtc, DateTime EndUtc) b) =>
        a.StartUtc <= b.EndUtc && b.StartUtc <= a.EndUtc;

    /// <summary>Thứ Hai của tuần chứa <paramref name="date"/>.</summary>
    public static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private static DateTime ToUtc(DateOnly date, TimeOnly time) =>
        DateTime.SpecifyKind(date.ToDateTime(time) - FacilityClock.Offset, DateTimeKind.Utc);
}
