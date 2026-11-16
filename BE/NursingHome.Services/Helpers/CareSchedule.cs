namespace NursingHome.Services.Helpers;

/// <summary>S3-H4: kiểm tra ngày trong tuần cho CarePlanItem.DaysOfWeek ('1010100' = T2, T4, T6).</summary>
public static class CareSchedule
{
    public static bool IsScheduledForDate(string? daysOfWeek, DateOnly date)
    {
        if (string.IsNullOrEmpty(daysOfWeek) || daysOfWeek.Length != 7)
        {
            return false;
        }

        int index = ((int)date.DayOfWeek + 6) % 7; // Monday = 0 ... Sunday = 6
        return daysOfWeek[index] == '1';
    }
}
