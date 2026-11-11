namespace NursingHomeManagement.Services;

public sealed class FacilityClock
{
    // The facility uses Vietnam time. Events are persisted as UTC instants.
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);
    public DateOnly Today => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(Offset).DateTime);

    public DateTimeOffset At(DateOnly date, TimeOnly time) => new DateTimeOffset(date.ToDateTime(time), Offset).ToUniversalTime();
    public DateTimeOffset StartOfDay(DateOnly date) => At(date, TimeOnly.MinValue);
}
