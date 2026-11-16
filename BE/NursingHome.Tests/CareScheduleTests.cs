using NursingHome.Services.Helpers;

namespace NursingHome.Tests;

public class CareScheduleTests
{
    [Theory]
    [InlineData("1010100", 2026, 11, 16, true)]  // Thứ Hai
    [InlineData("1010100", 2026, 11, 17, false)] // Thứ Ba
    [InlineData("1010100", 2026, 11, 18, true)]  // Thứ Tư
    [InlineData("1010100", 2026, 11, 19, false)] // Thứ Năm
    [InlineData("1010100", 2026, 11, 20, true)]  // Thứ Sáu
    [InlineData("1010100", 2026, 11, 21, false)] // Thứ Bảy
    [InlineData("1010100", 2026, 11, 22, false)] // Chủ Nhật
    [InlineData("0000001", 2026, 11, 22, true)]  // Chỉ Chủ Nhật
    public void IsScheduledForDate_MatchesMondayFirstPattern(string pattern, int year, int month, int day, bool expected)
    {
        Assert.Equal(expected, CareSchedule.IsScheduledForDate(pattern, new DateOnly(year, month, day)));
    }
}
