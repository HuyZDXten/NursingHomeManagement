using System.Data;
using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.DataAccessObjects;

/// <summary>Tổng khách đang giữ chỗ của một ngày + buổi.</summary>
public sealed record TourSlotUsage(DateOnly VisitDate, TourSlot TimeSlot, int Guests);

/// <summary>MODULE VẬN HÀNH (Viên) — S3-V1: yêu cầu tư vấn + lịch tham quan.</summary>
public sealed class AdmissionDAO : GenericDAO<Inquiry>
{
    public static AdmissionDAO Instance { get; } = new();

    private AdmissionDAO()
    {
    }

    // ---------------- Yêu cầu tư vấn ----------------

    public async Task<List<Inquiry>> ListInquiriesAsync()
    {
        await using var context = CreateContext();
        return await WithDetails(context.Inquiries.AsNoTracking())
            .OrderByDescending(inquiry => inquiry.CreatedAt)
            .ToListAsync();
    }

    /// <summary>Bản đầy đủ để trả DTO (không dùng để Update vì kéo theo cả đồ thị).</summary>
    public async Task<Inquiry?> GetInquiryDetailAsync(int id)
    {
        await using var context = CreateContext();
        return await WithDetails(context.Inquiries.AsNoTracking()).FirstOrDefaultAsync(inquiry => inquiry.Id == id);
    }

    /// <summary>Bản rời (không navigation) để sửa rồi gọi UpdateAsync.</summary>
    public Task<Inquiry?> GetInquiryAsync(int id) => GetByIdAsync(id);

    // ---------------- Lịch tham quan ----------------

    public async Task<List<TourBooking>> ListToursAsync(DateOnly from, DateOnly to)
    {
        await using var context = CreateContext();
        return await context.TourBookings.AsNoTracking()
            .Include(tour => tour.Inquiry)
            .Include(tour => tour.ConfirmedBy)
            .Where(tour => tour.VisitDate >= from && tour.VisitDate <= to)
            .OrderBy(tour => tour.VisitDate).ThenBy(tour => tour.TimeSlot).ThenBy(tour => tour.Id)
            .ToListAsync();
    }

    public async Task<TourBooking?> GetTourDetailAsync(int id)
    {
        await using var context = CreateContext();
        return await context.TourBookings.AsNoTracking()
            .Include(tour => tour.Inquiry)
            .Include(tour => tour.ConfirmedBy)
            .FirstOrDefaultAsync(tour => tour.Id == id);
    }

    public async Task<TourBooking?> GetTourAsync(int id)
    {
        await using var context = CreateContext();
        return await context.TourBookings.FindAsync(id);
    }

    public Task UpdateTourAsync(TourBooking tour) => UpdateEntityAsync(tour);

    /// <summary>Tổng khách giữ chỗ (Requested/Confirmed) theo ngày + buổi trong [from, to].</summary>
    public async Task<List<TourSlotUsage>> CountGuestsAsync(DateOnly from, DateOnly to)
    {
        await using var context = CreateContext();
        return await context.TourBookings.AsNoTracking()
            .Where(tour => tour.VisitDate >= from && tour.VisitDate <= to
                && (tour.Status == TourStatus.Requested || tour.Status == TourStatus.Confirmed))
            .GroupBy(tour => new { tour.VisitDate, tour.TimeSlot })
            .Select(group => new TourSlotUsage(group.Key.VisitDate, group.Key.TimeSlot, group.Sum(tour => tour.GuestCount)))
            .ToListAsync();
    }

    /// <summary>
    /// Đặt lịch: kiểm sức chứa và ghi trong CÙNG một transaction Serializable để hai người đặt chỗ cuối
    /// cùng lúc không vượt sức chứa (người đến sau nhận lỗi hết chỗ hoặc 409 do deadlock).
    /// <paramref name="tour"/>.Inquiry là yêu cầu mới (được thêm cùng lúc), hoặc đặt sẵn InquiryId của yêu cầu có sẵn.
    /// </summary>
    public async Task<TourBooking> BookTourAsync(TourBooking tour, int capacity)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var used = await context.TourBookings
            .Where(item => item.VisitDate == tour.VisitDate && item.TimeSlot == tour.TimeSlot
                && (item.Status == TourStatus.Requested || item.Status == TourStatus.Confirmed))
            .SumAsync(item => (int?)item.GuestCount) ?? 0;
        var remaining = Math.Max(0, capacity - used);
        if (tour.GuestCount > remaining)
        {
            throw new BusinessRuleException(remaining == 0
                ? "Buổi tham quan này đã kín chỗ. Vui lòng chọn ngày hoặc buổi khác."
                : $"Buổi tham quan này chỉ còn {remaining} chỗ. Vui lòng giảm số khách hoặc chọn buổi khác.");
        }

        context.TourBookings.Add(tour);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return tour;
    }

    /// <summary>Đánh dấu khách đã đến; yêu cầu đang New/Contacted chuyển Visited trong cùng lần lưu.</summary>
    public async Task CompleteTourAsync(int tourId, DateTime now)
    {
        await using var context = CreateContext();
        var tour = await context.TourBookings.Include(item => item.Inquiry).FirstOrDefaultAsync(item => item.Id == tourId)
            ?? throw new NotFoundException("Không tìm thấy lịch tham quan.");
        tour.Status = TourStatus.Done;
        tour.UpdatedAt = now;
        if (tour.Inquiry.Status is InquiryStatus.New or InquiryStatus.Contacted)
        {
            tour.Inquiry.Status = InquiryStatus.Visited;
            tour.Inquiry.UpdatedAt = now;
        }

        await context.SaveChangesAsync();
    }

    private static IQueryable<Inquiry> WithDetails(IQueryable<Inquiry> query) => query
        .Include(inquiry => inquiry.InterestedPackage)
        .Include(inquiry => inquiry.AssignedTo)
        .Include(inquiry => inquiry.Resident)
        .Include(inquiry => inquiry.Tours)
        .AsSplitQuery();
}
