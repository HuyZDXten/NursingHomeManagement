using NursingHome.BusinessObjects.Entities;
using NursingHome.Services.Helpers;

namespace NursingHome.Tests;

/// <summary>S3-V2: khung giờ ca (ca đêm qua ngày), ca liền kề, đầu tuần.</summary>
public class StaffingRuleTests
{
    private static readonly Shift Morning = new() { Code = Shift.MorningCode, StartTime = new(6, 0), EndTime = new(14, 0) };
    private static readonly Shift Afternoon = new() { Code = Shift.AfternoonCode, StartTime = new(14, 0), EndTime = new(22, 0) };
    private static readonly Shift Night = new() { Code = Shift.NightCode, StartTime = new(22, 0), EndTime = new(6, 0) };

    [Fact]
    public void MorningShift_IsSameDay_InUtc()
    {
        var (start, end) = StaffingRules.ShiftWindow(new DateOnly(2026, 11, 16), Morning);
        Assert.Equal(new DateTime(2026, 11, 15, 23, 0, 0, DateTimeKind.Utc), start); // 06:00 giờ VN
        Assert.Equal(new DateTime(2026, 11, 16, 7, 0, 0, DateTimeKind.Utc), end);
        Assert.Equal(DateTimeKind.Utc, start.Kind);
    }

    [Fact]
    public void NightShift_EndsNextMorning()
    {
        var (start, end) = StaffingRules.ShiftWindow(new DateOnly(2026, 11, 16), Night);
        Assert.True(Night.CrossesMidnight);
        Assert.Equal(TimeSpan.FromHours(8), end - start);
        Assert.Equal(new DateTime(2026, 11, 16, 23, 0, 0, DateTimeKind.Utc), end); // 06:00 ngày 17 giờ VN
    }

    [Fact]
    public void ConsecutiveShifts_Touch_ButFarShiftsDoNot()
    {
        var day = new DateOnly(2026, 11, 16);
        var morning = StaffingRules.ShiftWindow(day, Morning);
        Assert.True(StaffingRules.Touches(morning, StaffingRules.ShiftWindow(day, Afternoon)));
        Assert.False(StaffingRules.Touches(morning, StaffingRules.ShiftWindow(day, Night)));
        // Ca đêm hôm trước kết thúc 06:00 đúng lúc ca sáng bắt đầu.
        Assert.True(StaffingRules.Touches(morning, StaffingRules.ShiftWindow(day.AddDays(-1), Night)));
    }

    [Theory]
    [InlineData("2026-11-16", "2026-11-16")] // thứ Hai
    [InlineData("2026-11-19", "2026-11-16")]
    [InlineData("2026-11-22", "2026-11-16")] // Chủ Nhật thuộc tuần bắt đầu thứ Hai trước đó
    public void WeekStart_IsMonday(string date, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), StaffingRules.WeekStart(DateOnly.Parse(date)));
}
