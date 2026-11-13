using NursingHome.BusinessObjects.Entities;
using NursingHome.DTOs.Staffing;
using NursingHome.Services.Helpers;
using NursingHome.Services.Implementations;

namespace NursingHome.Tests;

/// <summary>S3-V4: ca kế tiếp (người được xác nhận bàn giao), khoảng nghỉ phép, ca đã bắt đầu, JSON mục bàn giao.</summary>
public class HandoverRuleTests
{
    private static readonly Shift Morning = new() { Id = 1, Code = Shift.MorningCode, Name = "Ca sáng", StartTime = new(6, 0), EndTime = new(14, 0) };
    private static readonly Shift Afternoon = new() { Id = 2, Code = Shift.AfternoonCode, Name = "Ca chiều", StartTime = new(14, 0), EndTime = new(22, 0) };
    private static readonly Shift Night = new() { Id = 3, Code = Shift.NightCode, Name = "Ca đêm", StartTime = new(22, 0), EndTime = new(6, 0) };
    private static readonly Shift[] All = [Night, Afternoon, Morning];

    [Theory]
    [InlineData(1, 0, 2)] // sáng → chiều cùng ngày
    [InlineData(2, 0, 3)] // chiều → đêm cùng ngày
    [InlineData(3, 1, 1)] // đêm → sáng hôm sau
    public void NextShift_FollowsTheClock(int shiftId, int dayOffset, int expectedShiftId)
    {
        var day = new DateOnly(2026, 11, 16);
        var next = StaffingRules.NextShift(day, All.Single(shift => shift.Id == shiftId), All);
        Assert.NotNull(next);
        Assert.Equal(day.AddDays(dayOffset), next.Value.WorkDate);
        Assert.Equal(expectedShiftId, next.Value.Shift.Id);
    }

    [Fact]
    public void NextShift_IsNull_WhenCatalogHasAGap()
    {
        var next = StaffingRules.NextShift(new DateOnly(2026, 11, 16), Morning, [Morning, Night]);
        Assert.Null(next);
    }

    [Fact]
    public void ValidateLeaveRange_RejectsPastReversedAndTooLong()
    {
        var today = new DateOnly(2026, 11, 16);
        Assert.Null(HandoverService.ValidateLeaveRange(today, today, today));
        Assert.Null(HandoverService.ValidateLeaveRange(today, today.AddDays(HandoverService.MaxLeaveDays - 1), today));
        Assert.NotNull(HandoverService.ValidateLeaveRange(today.AddDays(-1), today, today));
        Assert.NotNull(HandoverService.ValidateLeaveRange(today.AddDays(3), today.AddDays(2), today));
        Assert.NotNull(HandoverService.ValidateLeaveRange(today, today.AddDays(HandoverService.MaxLeaveDays), today));
    }

    [Fact]
    public void HasStarted_UsesFacilityTime()
    {
        var assignment = new ShiftAssignment { WorkDate = new DateOnly(2026, 11, 16), Shift = Morning };
        // Ca sáng 06:00 giờ VN = 23:00 UTC hôm trước.
        Assert.False(HandoverService.HasStarted(assignment, new DateTime(2026, 11, 15, 22, 59, 0, DateTimeKind.Utc)));
        Assert.True(HandoverService.HasStarted(assignment, new DateTime(2026, 11, 15, 23, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Highlights_RoundTripAsCamelCaseJson()
    {
        var json = OperationsMapper.WriteHighlights([new HandoverItemDto { Kind = "Task", ResidentId = 1, ResidentName = "NH-0001", Text = "Chưa xong: đo sinh hiệu." }]);
        Assert.NotNull(json);
        Assert.Contains("\"residentId\":1", json);
        var items = OperationsMapper.ReadHighlights(json);
        Assert.Equal("Chưa xong: đo sinh hiệu.", Assert.Single(items).Text);
        Assert.Null(OperationsMapper.WriteHighlights([]));
        Assert.Empty(OperationsMapper.ReadHighlights("không phải json"));
    }
}
