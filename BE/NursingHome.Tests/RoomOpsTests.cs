using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DataAccessObjects;
using NursingHome.Services.Helpers;

namespace NursingHome.Tests;

/// <summary>BL-V3: phụ thu loại phòng, hạn giữ giường, luồng phiếu sửa chữa.</summary>
public class RoomOpsTests
{
    private static readonly DateOnly Start = new(2026, 12, 1);
    private static readonly DateOnly End = new(2026, 12, 31);

    /// <summary>Thời điểm UTC ứng với giờ cơ sở (UTC+7) của ngày + giờ.</summary>
    private static DateTime Local(int month, int day, int hour = 9) => new DateTime(2026, month, day, hour, 0, 0, DateTimeKind.Utc).AddHours(-7);

    private static SurchargeStay Stay(DateTime start, DateTime? end, decimal price = 150000, string type = "Phòng đơn VIP", string room = "202") =>
        new(start, end, room, type, price);

    [Fact]
    public void Surcharge_OpenStayCoversWholeMonth()
    {
        var line = Assert.Single(RoomRules.SurchargeLines(Start, End, [Stay(Local(11, 20), null)]));
        Assert.Equal(31, line.Days);
        Assert.Equal(150000, line.DailySurcharge);
        Assert.Equal("Phụ thu Phòng đơn VIP (phòng 202): 01/12–31/12 (31 ngày)", line.Description);
    }

    [Fact]
    public void Surcharge_TransferDayCountsForNewRoomOnly()
    {
        // Ở phòng đôi tới sáng 10/12 thì chuyển lên VIP: phòng đôi 01–09 (9 ngày), VIP 10–31 (22 ngày), không trùng ngày 10.
        var lines = RoomRules.SurchargeLines(Start, End,
        [
            Stay(Local(11, 1), Local(12, 10, 8), 50000, "Phòng đôi tiện nghi", "102"),
            Stay(Local(12, 10, 8), null)
        ]);
        Assert.Equal([9, 22], lines.Select(item => item.Days));
        Assert.Equal(31, lines.Sum(item => item.Days));
    }

    [Fact]
    public void Surcharge_SkipsStaysOutsidePeriodAndFreeRooms()
    {
        Assert.Empty(RoomRules.SurchargeLines(Start, End, [Stay(Local(10, 1), Local(11, 30, 20))]));
        Assert.Empty(RoomRules.SurchargeLines(Start, End, [Stay(Local(12, 5), null, price: 0)]));
        // Vào phòng rồi rời ngay trong ngày: không tính ngày nào.
        Assert.Empty(RoomRules.SurchargeLines(Start, End, [Stay(Local(12, 5, 8), Local(12, 5, 17))]));
    }

    [Fact]
    public void Surcharge_MidMonthStayAndDischarge()
    {
        var line = Assert.Single(RoomRules.SurchargeLines(Start, End, [Stay(Local(12, 15, 14), Local(12, 20, 10))]));
        Assert.Equal(5, line.Days); // đêm 15, 16, 17, 18, 19
        Assert.Contains("15/12–19/12", line.Description);
    }

    [Fact]
    public void Reservation_ExpiryWithinSevenDaysEndOfLocalDay()
    {
        var clock = new FacilityClock(TimeProvider.System);
        var today = new DateOnly(2026, 11, 17);
        var expires = RoomRules.ReservationExpiry(today.AddDays(3), today, clock);
        Assert.Equal(new DateTime(2026, 11, 20, 17, 0, 0, DateTimeKind.Utc), expires); // hết 20/11 giờ VN = 17:00 UTC
        Assert.Throws<BusinessRuleException>(() => RoomRules.ReservationExpiry(today.AddDays(-1), today, clock));
        Assert.Throws<BusinessRuleException>(() => RoomRules.ReservationExpiry(today.AddDays(8), today, clock));
        RoomRules.ReservationExpiry(today.AddDays(7), today, clock);
    }

    [Theory]
    [InlineData(MaintenanceStatus.Open, MaintenanceStatus.InProgress, true)]
    [InlineData(MaintenanceStatus.Open, MaintenanceStatus.Done, true)]
    [InlineData(MaintenanceStatus.InProgress, MaintenanceStatus.Cancelled, true)]
    [InlineData(MaintenanceStatus.InProgress, MaintenanceStatus.Open, false)]
    [InlineData(MaintenanceStatus.Done, MaintenanceStatus.InProgress, false)]
    [InlineData(MaintenanceStatus.Cancelled, MaintenanceStatus.Done, false)]
    [InlineData(MaintenanceStatus.Open, MaintenanceStatus.Open, false)]
    public void Maintenance_FlowIsForwardOnly(MaintenanceStatus from, MaintenanceStatus to, bool allowed)
    {
        Assert.Equal(allowed, MaintenanceFlow.CanMove(from, to));
    }
}
