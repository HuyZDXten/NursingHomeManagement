using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DataAccessObjects;

namespace NursingHome.Services.Helpers;

/// <summary>Một dòng phụ thu phòng trên hóa đơn: Quantity = số ngày, UnitPrice = phụ thu/ngày.</summary>
public sealed record SurchargeLine(string Description, int Days, decimal DailySurcharge);

/// <summary>BL-V3: phụ thu loại phòng (UC-36) và giữ giường (UC-41), viết thành hàm thuần để unit test.</summary>
public static class RoomRules
{
    public const int MaxReservationDays = 7;

    /// <summary>
    /// Phụ thu tính theo NGÀY Ở trong kỳ [periodStart, periodEnd] (giờ cơ sở): một lần ở tính từ ngày vào phòng tới
    /// HẾT ngày trước ngày rời phòng (ngày chuyển phòng tính cho phòng mới, không tính trùng). Lần ở chưa kết thúc tính
    /// tới hết kỳ. Giá lấy theo loại phòng lúc lập hóa đơn.
    /// </summary>
    public static List<SurchargeLine> SurchargeLines(DateOnly periodStart, DateOnly periodEnd, IEnumerable<SurchargeStay> stays)
    {
        var lines = new List<SurchargeLine>();
        foreach (var stay in stays)
        {
            var from = FacilityClock.ToLocalDate(stay.StartedAt);
            var toExclusive = stay.EndedAt is { } ended ? FacilityClock.ToLocalDate(ended) : periodEnd.AddDays(1);
            var start = from > periodStart ? from : periodStart;
            var endExclusive = toExclusive < periodEnd.AddDays(1) ? toExclusive : periodEnd.AddDays(1);
            int days = endExclusive.DayNumber - start.DayNumber;
            if (days <= 0 || stay.DailySurcharge <= 0)
            {
                continue;
            }

            var last = endExclusive.AddDays(-1);
            lines.Add(new SurchargeLine(
                $"Phụ thu {stay.RoomTypeName} (phòng {stay.RoomNumber}): {start:dd/MM}–{last:dd/MM} ({days} ngày)",
                days,
                stay.DailySurcharge));
        }

        return lines;
    }

    public static void EnsureWholeVnd(decimal amount)
    {
        if (decimal.Round(amount, 0) != amount)
        {
            throw new BusinessRuleException("Phụ thu phải là số đồng (không có phần lẻ).");
        }
    }

    /// <summary>Giữ giường tới hết ngày <paramref name="expiresOn"/> (giờ cơ sở): không trước hôm nay, tối đa 7 ngày tới.</summary>
    public static DateTime ReservationExpiry(DateOnly expiresOn, DateOnly today, FacilityClock clock)
    {
        if (expiresOn < today)
        {
            throw new BusinessRuleException("Ngày hết giữ không được trước hôm nay.");
        }

        if (expiresOn > today.AddDays(MaxReservationDays))
        {
            throw new BusinessRuleException($"Giữ giường tối đa {MaxReservationDays} ngày.");
        }

        return clock.StartOfDayUtc(expiresOn.AddDays(1));
    }
}
