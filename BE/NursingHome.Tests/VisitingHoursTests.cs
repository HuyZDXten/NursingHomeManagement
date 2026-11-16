using NursingHome.Services.Helpers;

namespace NursingHome.Tests;

public class VisitingHoursTests
{
    [Fact]
    public void Slots_SplitsMorningAndAfternoonRangesIntoOneHourSlots()
    {
        var slots = VisitingHours.Slots("08:00-11:00, 14:00-16:00");
        Assert.Equal(5, slots.Count);
        Assert.Equal((new TimeOnly(8, 0), new TimeOnly(9, 0)), slots[0]);
        Assert.Equal((new TimeOnly(10, 0), new TimeOnly(11, 0)), slots[2]);
        Assert.Equal((new TimeOnly(14, 0), new TimeOnly(15, 0)), slots[3]);
        Assert.Equal((new TimeOnly(15, 0), new TimeOnly(16, 0)), slots[4]);
    }
}
