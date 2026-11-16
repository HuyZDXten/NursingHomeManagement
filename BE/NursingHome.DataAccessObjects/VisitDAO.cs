using System.Data;
using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.DataAccessObjects;

/// <summary>S3-H3 (UC-103, UC-104): đặt &amp; duyệt lịch thăm.</summary>
public sealed class VisitDAO : GenericDAO<VisitBooking>
{
    public static VisitDAO Instance { get; } = new();

    private VisitDAO()
    {
    }

    public async Task<Dictionary<TimeOnly, int>> CountGuestsAsync(DateOnly date)
    {
        await using var context = CreateContext();
        var rows = await context.VisitBookings.AsNoTracking()
            .Where(item => item.VisitDate == date && (item.Status == VisitStatus.Requested || item.Status == VisitStatus.Approved))
            .GroupBy(item => item.SlotStart)
            .Select(group => new { SlotStart = group.Key, Total = group.Sum(x => x.GuestCount) })
            .ToListAsync();
        return rows.ToDictionary(x => x.SlotStart, x => x.Total);
    }

    /// <summary>
    /// Đặt lịch trong transaction Serializable: kiểm tra sức chứa khung giờ và giới hạn tối đa 1 lịch/ngày/tài khoản.
    /// </summary>
    public async Task<VisitBooking> BookAsync(VisitBooking booking, int capacity)
    {
        await using var context = CreateContext();
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        bool hasSameDay = await context.VisitBookings.AnyAsync(item =>
            item.FamilyAccountId == booking.FamilyAccountId &&
            item.VisitDate == booking.VisitDate &&
            (item.Status == VisitStatus.Requested || item.Status == VisitStatus.Approved || item.Status == VisitStatus.CheckedIn));
        if (hasSameDay)
        {
            throw new BusinessRuleException("Mỗi tài khoản gia đình chỉ được đặt tối đa 1 lịch thăm đang hiệu lực mỗi ngày.");
        }

        int booked = await context.VisitBookings
            .Where(item => item.VisitDate == booking.VisitDate &&
                           item.SlotStart == booking.SlotStart &&
                           (item.Status == VisitStatus.Requested || item.Status == VisitStatus.Approved))
            .SumAsync(item => (int?)item.GuestCount) ?? 0;

        if (booked + booking.GuestCount > capacity)
        {
            int remaining = Math.Max(0, capacity - booked);
            throw new BusinessRuleException($"Khung giờ này chỉ còn {remaining} chỗ. Vui lòng giảm số khách hoặc chọn khung khác.");
        }

        context.VisitBookings.Add(booking);
        await context.SaveChangesAsync();
        await tx.CommitAsync();

        return (await GetDetailAsync(booking.Id))!;
    }

    public async Task<List<VisitBooking>> ListForFamilyAsync(int familyAccountId)
    {
        await using var context = CreateContext();
        return await context.VisitBookings.AsNoTracking()
            .Include(item => item.Resident)
            .Include(item => item.FamilyAccount)
            .Include(item => item.ReviewedBy)
            .Where(item => item.FamilyAccountId == familyAccountId)
            .OrderByDescending(item => item.VisitDate)
            .ThenByDescending(item => item.SlotStart)
            .ToListAsync();
    }

    public async Task<List<VisitBooking>> ListByDateAsync(DateOnly date, VisitStatus? status)
    {
        await using var context = CreateContext();
        var query = context.VisitBookings.AsNoTracking()
            .Include(item => item.Resident)
            .Include(item => item.FamilyAccount)
            .Include(item => item.ReviewedBy)
            .Where(item => item.VisitDate == date);
        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status);
        }

        return await query.OrderBy(item => item.SlotStart).ThenBy(item => item.Id).ToListAsync();
    }

    public async Task<VisitBooking?> GetDetailAsync(int id)
    {
        await using var context = CreateContext();
        return await context.VisitBookings.AsNoTracking()
            .Include(item => item.Resident)
            .Include(item => item.FamilyAccount)
            .Include(item => item.ReviewedBy)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task UpdateStatusAsync(int id, VisitStatus[] expectedStatuses, Action<VisitBooking> apply)
    {
        await using var context = CreateContext();
        var booking = await context.VisitBookings.FindAsync(id)
            ?? throw new NotFoundException("Không tìm thấy lịch thăm.");
        if (!expectedStatuses.Contains(booking.Status))
        {
            throw new BusinessRuleException($"Không thể chuyển trạng thái từ {booking.Status}.");
        }

        apply(booking);
        await context.SaveChangesAsync();
    }
}
