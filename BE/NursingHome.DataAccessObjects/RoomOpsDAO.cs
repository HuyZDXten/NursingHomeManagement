using System.Data;
using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.DataAccessObjects;

/// <summary>Một lần ở phòng có phụ thu (UC-36), để BillingService cộng dòng phụ thu vào hóa đơn.</summary>
public sealed record SurchargeStay(DateTime StartedAt, DateTime? EndedAt, string RoomNumber, string RoomTypeName, decimal DailySurcharge);

/// <summary>
/// UC-41: kiểm tra giường đang được giữ trước khi xếp người, chạy BÊN TRONG transaction của nơi xếp giường
/// (RoomDAO.AssignAsync, ResidentRecordsDAO.ReadmitAsync).
/// </summary>
public static class BedReservationGuard
{
    /// <summary>
    /// Không có lượt giữ → cho xếp. Lượt giữ đã hết hạn → tự chuyển Released, cho xếp. Lượt giữ cho yêu cầu tư vấn
    /// mà cư dân này được nhập viện từ đó → chuyển Converted, cho xếp. Còn lại → chặn.
    /// </summary>
    public static async Task EnsureAssignableAsync(NursingHomeContext context, int bedId, int residentId, DateTime now)
    {
        var reservation = await context.BedReservations.Include(item => item.Inquiry)
            .FirstOrDefaultAsync(item => item.BedId == bedId && item.Status == ReservationStatus.Active);
        if (reservation is null)
        {
            return;
        }

        if (reservation.ExpiresAt <= now)
        {
            reservation.Status = ReservationStatus.Released;
            reservation.ClosedAt = now;
            return;
        }

        if (reservation.Inquiry?.ResidentId == residentId)
        {
            reservation.Status = ReservationStatus.Converted;
            reservation.ClosedAt = now;
            reservation.ResidentId = residentId;
            return;
        }

        var until = reservation.ExpiresAt + TimeSpan.FromHours(7);
        throw new BusinessRuleException($"Giường đang được giữ cho {reservation.HolderName} đến {until:dd/MM HH:mm}. Hủy giữ giường trước khi xếp người khác.");
    }
}

/// <summary>MODULE VẬN HÀNH (Viên) — BL-V3: loại phòng (UC-36), giữ giường (UC-41), báo hỏng & sửa chữa (UC-42).</summary>
public sealed class RoomOpsDAO : GenericDAO<RoomType>
{
    public static RoomOpsDAO Instance { get; } = new();

    private RoomOpsDAO()
    {
    }

    // ---------------- Loại phòng ----------------

    public async Task<List<RoomType>> ListTypesAsync(bool activeOnly)
    {
        await using var context = CreateContext();
        return await context.RoomTypes.AsNoTracking().Where(item => !activeOnly || item.IsActive)
            .OrderBy(item => item.DailySurcharge).ThenBy(item => item.Name).ToListAsync();
    }

    public async Task<bool> TypeNameExistsAsync(string name, int? excludeId)
    {
        await using var context = CreateContext();
        return await context.RoomTypes.AnyAsync(item => item.Name == name && item.Id != excludeId);
    }

    /// <summary>Lần ở phòng có phụ thu giao với [fromUtc, toUtc).</summary>
    public async Task<List<SurchargeStay>> SurchargeStaysAsync(int residentId, DateTime fromUtc, DateTime toUtc)
    {
        await using var context = CreateContext();
        return await context.BedAssignments.AsNoTracking()
            .Where(item => item.ResidentId == residentId && item.StartedAt < toUtc && (item.EndedAt == null || item.EndedAt > fromUtc)
                && item.Bed.Room.RoomType != null && item.Bed.Room.RoomType.DailySurcharge > 0)
            .OrderBy(item => item.StartedAt)
            .Select(item => new SurchargeStay(item.StartedAt, item.EndedAt, item.Bed.Room.Number,
                item.Bed.Room.RoomType!.Name, item.Bed.Room.RoomType.DailySurcharge))
            .ToListAsync();
    }

    // ---------------- Giữ giường ----------------

    public async Task<List<BedReservation>> ListReservationsAsync(bool activeOnly, DateTime now)
    {
        await using var context = CreateContext();
        var query = context.BedReservations.AsNoTracking()
            .Include(item => item.Bed).ThenInclude(bed => bed.Room)
            .Include(item => item.ReservedBy)
            .AsQueryable();
        if (activeOnly)
        {
            query = query.Where(item => item.Status == ReservationStatus.Active && item.ExpiresAt > now);
        }

        return await query.OrderByDescending(item => item.ReservedAt).Take(200).ToListAsync();
    }

    /// <summary>Giữ giường trống: phòng đang dùng, giường Available, không ai ở, chưa có lượt giữ còn hạn.</summary>
    public async Task<BedReservation> CreateReservationAsync(BedReservation reservation, DateTime now)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var bed = await context.Beds.Include(item => item.Room).FirstOrDefaultAsync(item => item.Id == reservation.BedId)
            ?? throw new NotFoundException("Không tìm thấy giường.");
        if (!bed.Room.IsActive || bed.Status != BedStatus.Available
            || await context.BedAssignments.AnyAsync(item => item.BedId == bed.Id && item.EndedAt == null))
        {
            throw new BusinessRuleException("Chỉ giữ được giường đang trống trong phòng đang sử dụng.");
        }

        var current = await context.BedReservations.FirstOrDefaultAsync(item => item.BedId == bed.Id && item.Status == ReservationStatus.Active);
        if (current is not null)
        {
            if (current.ExpiresAt > now)
            {
                throw new ConflictException($"Giường đang được giữ cho {current.HolderName}.");
            }

            current.Status = ReservationStatus.Released;
            current.ClosedAt = now;
            await context.SaveChangesAsync();
        }

        if (reservation.InquiryId is { } inquiryId)
        {
            var inquiry = await context.Inquiries.FindAsync(inquiryId) ?? throw new NotFoundException("Không tìm thấy yêu cầu tư vấn.");
            if (inquiry.Status is InquiryStatus.Admitted or InquiryStatus.Lost)
            {
                throw new BusinessRuleException("Yêu cầu tư vấn đã kết thúc, không giữ giường được.");
            }
        }

        context.BedReservations.Add(reservation);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return reservation;
    }

    public async Task<BedReservation?> GetReservationAsync(int id)
    {
        await using var context = CreateContext();
        return await context.BedReservations.AsNoTracking()
            .Include(item => item.Bed).ThenInclude(bed => bed.Room)
            .Include(item => item.ReservedBy)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task<bool> ReleaseReservationAsync(int id, int byAccountId, DateTime now)
    {
        await using var context = CreateContext();
        int changed = await context.BedReservations.Where(item => item.Id == id && item.Status == ReservationStatus.Active)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, ReservationStatus.Released)
                .SetProperty(item => item.ClosedAt, now)
                .SetProperty(item => item.ClosedById, byAccountId));
        return changed == 1;
    }

    // ---------------- Báo hỏng & sửa chữa ----------------

    public async Task<List<MaintenanceRequest>> ListMaintenanceAsync(MaintenanceStatus? status, int? roomId)
    {
        await using var context = CreateContext();
        var query = context.MaintenanceRequests.AsNoTracking()
            .Include(item => item.Room).Include(item => item.Bed).Include(item => item.ReportedBy).AsQueryable();
        if (status is { } s)
        {
            query = query.Where(item => item.Status == s);
        }

        if (roomId is { } r)
        {
            query = query.Where(item => item.RoomId == r);
        }

        return await query.OrderBy(item => item.Status == MaintenanceStatus.Done || item.Status == MaintenanceStatus.Cancelled)
            .ThenByDescending(item => item.Priority == MaintenancePriority.High)
            .ThenByDescending(item => item.ReportedAt).Take(300).ToListAsync();
    }

    public async Task<MaintenanceRequest?> GetMaintenanceAsync(int id)
    {
        await using var context = CreateContext();
        return await context.MaintenanceRequests.AsNoTracking()
            .Include(item => item.Room).Include(item => item.Bed).Include(item => item.ReportedBy)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    /// <summary>Phiếu chặn giường + giường đang trống → giường chuyển Maintenance (giường có người ở thì giữ nguyên).</summary>
    public async Task<MaintenanceRequest> CreateMaintenanceAsync(MaintenanceRequest request, DateTime now)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        _ = await context.Rooms.FindAsync(request.RoomId) ?? throw new NotFoundException("Không tìm thấy phòng.");
        if (request.BedId is { } bedId)
        {
            var bed = await context.Beds.FindAsync(bedId);
            if (bed is null || bed.RoomId != request.RoomId)
            {
                throw new BusinessRuleException("Giường không thuộc phòng đã chọn.");
            }

            if (request.BlocksBed && bed.Status == BedStatus.Available)
            {
                bed.Status = BedStatus.Maintenance;
                bed.UpdatedAt = now;
            }
        }

        context.MaintenanceRequests.Add(request);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return request;
    }

    /// <summary>
    /// Đổi trạng thái theo <see cref="MaintenanceFlow"/>. Xong/hủy phiếu chặn giường: nếu không còn phiếu chặn nào khác
    /// đang mở cho giường đó và giường đang Maintenance → trả về Available.
    /// </summary>
    public async Task UpdateMaintenanceAsync(int id, MaintenanceStatus status, string? assignedTo, string? note, int byAccountId, DateTime now)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var request = await context.MaintenanceRequests.FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new NotFoundException("Không tìm thấy phiếu sửa chữa.");
        if (!MaintenanceFlow.CanMove(request.Status, status))
        {
            throw new BusinessRuleException("Không chuyển được sang trạng thái này (phiếu đã xong/hủy hoặc chuyển ngược).");
        }

        request.Status = status;
        if (assignedTo is not null)
        {
            request.AssignedTo = assignedTo;
        }

        if (MaintenanceFlow.IsFinal(status))
        {
            request.ResolvedAt = now;
            request.ResolvedById = byAccountId;
            request.ResolutionNote = note;
            if (request.BlocksBed && request.BedId is { } bedId)
            {
                bool otherBlocking = await context.MaintenanceRequests.AnyAsync(item => item.Id != id && item.BedId == bedId && item.BlocksBed
                    && (item.Status == MaintenanceStatus.Open || item.Status == MaintenanceStatus.InProgress));
                var bed = await context.Beds.FindAsync(bedId);
                if (!otherBlocking && bed is { Status: BedStatus.Maintenance })
                {
                    bed.Status = BedStatus.Available;
                    bed.UpdatedAt = now;
                }
            }
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
