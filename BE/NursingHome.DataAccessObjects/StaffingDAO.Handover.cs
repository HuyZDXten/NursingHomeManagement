using System.Data;
using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.DataAccessObjects;

/// <summary>Dữ liệu thô cho gợi ý bàn giao ca (S3-V4). Tên cư dân của sinh hiệu lấy riêng vì VitalSign không có navigation.</summary>
public sealed record HandoverDraftData(
    List<CareTask> OpenTasks,
    List<MedicationLog> MissedMedications,
    List<VitalSign> Vitals,
    Dictionary<int, (string Code, string FullName)> Residents);

/// <summary>MODULE VẬN HÀNH (Viên) — S3-V4: nghỉ phép/đổi ca (UC-47) và bàn giao ca (UC-49).</summary>
public sealed partial class StaffingDAO
{
    private const string NotPendingMessage = "Đơn không còn chờ duyệt (đã được xử lý hoặc đã hủy).";

    // ---------------- Đơn nghỉ phép / đổi ca ----------------

    /// <param name="involvingAccountId">Có giá trị = chỉ đơn của người này hoặc đơn xin đổi ca với người này.</param>
    public async Task<List<LeaveRequest>> ListLeaveRequestsAsync(int? involvingAccountId, LeaveRequestStatus? status, int take)
    {
        await using var context = CreateContext();
        return await IncludeLeaveDetails(context.LeaveRequests.AsNoTracking())
            .Where(item => (involvingAccountId == null || item.AccountId == involvingAccountId || item.SwapWithAccountId == involvingAccountId)
                && (status == null || item.Status == status))
            .OrderByDescending(item => item.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<LeaveRequest?> GetLeaveRequestAsync(int id)
    {
        await using var context = CreateContext();
        return await IncludeLeaveDetails(context.LeaveRequests.AsNoTracking()).FirstOrDefaultAsync(item => item.Id == id);
    }

    /// <summary>Ca này đang nằm trong một đơn đổi ca chờ duyệt (của bất kỳ ai).</summary>
    public async Task<bool> HasPendingSwapAsync(int assignmentId)
    {
        await using var context = CreateContext();
        return await context.LeaveRequests.AnyAsync(item => item.Status == LeaveRequestStatus.Pending
            && (item.ShiftAssignmentId == assignmentId || item.SwapShiftAssignmentId == assignmentId));
    }

    public async Task<bool> HasPendingLeaveOverlapAsync(int accountId, DateOnly from, DateOnly to)
    {
        await using var context = CreateContext();
        return await context.LeaveRequests.AnyAsync(item => item.AccountId == accountId && item.Type == LeaveRequestType.Leave
            && item.Status == LeaveRequestStatus.Pending && item.FromDate <= to && from <= item.ToDate);
    }

    public Task<LeaveRequest> AddLeaveRequestAsync(LeaveRequest request) => AddEntityAsync(request);

    /// <summary>Từ chối / hủy: chỉ đổi được khi đơn còn Pending (câu UPDATE có điều kiện, duyệt 2 lần thì lần 2 bị chặn).</summary>
    public async Task SetLeaveStatusIfPendingAsync(int id, LeaveRequestStatus status, int? reviewerId, DateTime now, string? note)
    {
        await using var context = CreateContext();
        var updated = await context.LeaveRequests
            .Where(item => item.Id == id && item.Status == LeaveRequestStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, status)
                .SetProperty(item => item.ReviewedById, reviewerId)
                .SetProperty(item => item.ReviewedAt, now)
                .SetProperty(item => item.ReviewNote, note));
        if (updated == 0)
        {
            throw await context.LeaveRequests.AnyAsync(item => item.Id == id)
                ? new BusinessRuleException(NotPendingMessage)
                : new NotFoundException("Không tìm thấy đơn.");
        }
    }

    /// <summary>
    /// Duyệt đổi ca trong MỘT transaction Serializable: đơn còn Pending, 2 ca vẫn thuộc đúng 2 người, không ai bị trùng ô
    /// sau khi đổi → hoán AccountId. Đơn đổi ca khác đang chờ có dính 2 ca này tự hủy (chủ ca đã đổi).
    /// </summary>
    public async Task<List<LeaveRequest>> ApproveSwapAsync(int id, int reviewerId, DateTime now, string? note)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var request = await RequirePendingAsync(context, id, LeaveRequestType.Swap);
        var mine = await context.ShiftAssignments.FindAsync(request.ShiftAssignmentId);
        var theirs = await context.ShiftAssignments.FindAsync(request.SwapShiftAssignmentId);
        if (mine is null || theirs is null || mine.AccountId != request.AccountId || theirs.AccountId != request.SwapWithAccountId)
        {
            throw new BusinessRuleException("Lịch ca đã thay đổi sau khi tạo đơn nên không đổi được. Hãy từ chối đơn này.");
        }

        var clash = await context.ShiftAssignments.AnyAsync(item =>
            (item.AccountId == mine.AccountId && item.WorkDate == theirs.WorkDate && item.ShiftId == theirs.ShiftId)
            || (item.AccountId == theirs.AccountId && item.WorkDate == mine.WorkDate && item.ShiftId == mine.ShiftId));
        if (clash)
        {
            throw new BusinessRuleException("Sau khi đổi, một người sẽ bị trùng ca đã có. Hãy từ chối đơn này.");
        }

        (mine.AccountId, theirs.AccountId) = (theirs.AccountId, mine.AccountId);
        Review(request, LeaveRequestStatus.Approved, reviewerId, now, note);

        var stale = await context.LeaveRequests
            .Where(item => item.Id != id && item.Status == LeaveRequestStatus.Pending
                && (item.ShiftAssignmentId == mine.Id || item.ShiftAssignmentId == theirs.Id
                    || item.SwapShiftAssignmentId == mine.Id || item.SwapShiftAssignmentId == theirs.Id))
            .ToListAsync();
        foreach (var item in stale)
        {
            Review(item, LeaveRequestStatus.Cancelled, null, now, "Tự hủy: ca trong đơn đã được đổi theo đơn khác.");
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return stale;
    }

    /// <summary>
    /// Duyệt nghỉ phép trong MỘT transaction Serializable: bỏ các ca của người xin trong [FromDate, ToDate] mà
    /// <paramref name="canRemove"/> cho phép (ca chưa bắt đầu). Trả về các ca đã bỏ (có Shift) và đơn đổi ca tự hủy theo.
    /// </summary>
    public async Task<(List<ShiftAssignment> Removed, List<LeaveRequest> Cancelled)> ApproveLeaveAsync(
        int id, int reviewerId, DateTime now, string? note, Func<ShiftAssignment, bool> canRemove)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var request = await RequirePendingAsync(context, id, LeaveRequestType.Leave);
        var removed = (await context.ShiftAssignments.Include(item => item.Shift)
                .Where(item => item.AccountId == request.AccountId && item.WorkDate >= request.FromDate && item.WorkDate <= request.ToDate)
                .ToListAsync())
            .Where(canRemove)
            .ToList();
        var cancelled = await ReleaseAssignmentsAsync(context, removed.Select(item => item.Id).ToList(), now,
            "Tự hủy: ca trong đơn đã bị bỏ khi duyệt nghỉ phép.");
        context.ShiftAssignments.RemoveRange(removed);
        Review(request, LeaveRequestStatus.Approved, reviewerId, now, note);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return (removed, cancelled);
    }

    // ---------------- Bàn giao ca ----------------

    public async Task<bool> HandoverExistsAsync(DateOnly workDate, int shiftId, int fromAccountId)
    {
        await using var context = CreateContext();
        return await context.ShiftHandovers.AnyAsync(item =>
            item.WorkDate == workDate && item.ShiftId == shiftId && item.FromAccountId == fromAccountId);
    }

    /// <summary>Trùng (WorkDate, ShiftId, FromAccountId) bị UQ_ShiftHandovers chặn → 409.</summary>
    public Task<ShiftHandover> AddHandoverAsync(ShiftHandover handover) => AddEntityAsync(handover);

    public async Task<ShiftHandover?> GetHandoverAsync(long id)
    {
        await using var context = CreateContext();
        return await IncludeHandoverDetails(context.ShiftHandovers.AsNoTracking()).FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task<List<ShiftHandover>> ListHandoversAsync(DateOnly from, DateOnly to, bool unreadOnly)
    {
        await using var context = CreateContext();
        return await IncludeHandoverDetails(context.ShiftHandovers.AsNoTracking())
            .Where(item => item.WorkDate >= from && item.WorkDate <= to && (!unreadOnly || item.AcknowledgedById == null))
            .OrderByDescending(item => item.WorkDate).ThenByDescending(item => item.Shift.StartTime).ThenBy(item => item.FromAccount.FullName)
            .ToListAsync();
    }

    /// <summary>Chỉ người đầu tiên xác nhận được ghi (UPDATE có điều kiện).</summary>
    public async Task<bool> AcknowledgeHandoverAsync(long id, int accountId, DateTime now)
    {
        await using var context = CreateContext();
        return await context.ShiftHandovers
            .Where(item => item.Id == id && item.AcknowledgedById == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.AcknowledgedById, accountId)
                .SetProperty(item => item.AcknowledgedAt, now)) > 0;
    }

    /// <summary>
    /// Gom dữ liệu trong khung giờ ca [startUtc, endUtc): việc hạn trong ca chưa xong, liều thuốc Missed/Refused,
    /// sinh hiệu đo trong ca (Service lọc vượt ngưỡng). <paramref name="residentIds"/> NULL = mọi cư dân.
    /// </summary>
    public async Task<HandoverDraftData> GetHandoverDraftDataAsync(DateTime startUtc, DateTime endUtc, IReadOnlyCollection<int>? residentIds)
    {
        var ids = residentIds?.ToList();
        await using var context = CreateContext();
        var tasks = await context.CareTasks.AsNoTracking()
            .Include(item => item.Resident)
            .Include(item => item.AssignedTo)
            .Where(item => item.DueAt >= startUtc && item.DueAt < endUtc
                && (item.Status == CareTaskStatus.Pending || item.Status == CareTaskStatus.InProgress)
                && (ids == null || ids.Contains(item.ResidentId)))
            .OrderBy(item => item.DueAt)
            .ToListAsync();
        var medications = await context.MedicationLogs.AsNoTracking()
            .Include(item => item.MedicationSchedule).ThenInclude(schedule => schedule.Resident)
            .Where(item => item.ScheduledAt >= startUtc && item.ScheduledAt < endUtc
                && (item.Status == MedicationStatus.Missed || item.Status == MedicationStatus.Refused)
                && (ids == null || ids.Contains(item.MedicationSchedule.ResidentId)))
            .OrderBy(item => item.ScheduledAt)
            .ToListAsync();
        var vitals = await context.VitalSigns.AsNoTracking()
            .Where(item => item.RecordedAt >= startUtc && item.RecordedAt < endUtc && (ids == null || ids.Contains(item.ResidentId)))
            .OrderBy(item => item.RecordedAt)
            .ToListAsync();
        var vitalResidentIds = vitals.Select(item => item.ResidentId).Distinct().ToList();
        var residents = await context.Residents.AsNoTracking()
            .Where(item => vitalResidentIds.Contains(item.Id))
            .Select(item => new { item.Id, item.Code, item.FullName })
            .ToDictionaryAsync(item => item.Id, item => (item.Code, item.FullName));
        return new HandoverDraftData(tasks, medications, vitals, residents);
    }

    // ---------------- Nội bộ ----------------

    private static IQueryable<LeaveRequest> IncludeLeaveDetails(IQueryable<LeaveRequest> query) => query
        .Include(item => item.Account)
        .Include(item => item.SwapWithAccount)
        .Include(item => item.ReviewedBy)
        .Include(item => item.ShiftAssignment).ThenInclude(assignment => assignment!.Shift)
        .Include(item => item.SwapShiftAssignment).ThenInclude(assignment => assignment!.Shift);

    private static IQueryable<ShiftHandover> IncludeHandoverDetails(IQueryable<ShiftHandover> query) => query
        .Include(item => item.Shift)
        .Include(item => item.FromAccount)
        .Include(item => item.AcknowledgedBy);

    private static async Task<LeaveRequest> RequirePendingAsync(NursingHomeContext context, int id, LeaveRequestType type)
    {
        var request = await context.LeaveRequests.FirstOrDefaultAsync(item => item.Id == id && item.Type == type)
            ?? throw new NotFoundException("Không tìm thấy đơn.");
        return request.Status == LeaveRequestStatus.Pending ? request : throw new BusinessRuleException(NotPendingMessage);
    }

    private static void Review(LeaveRequest request, LeaveRequestStatus status, int? reviewerId, DateTime now, string? note)
    {
        request.Status = status;
        request.ReviewedById = reviewerId;
        request.ReviewedAt = now;
        request.ReviewNote = note;
    }

    /// <summary>
    /// Trước khi xóa ca: đơn đổi ca đang chờ có dính ca → Cancelled; mọi đơn (kể cả lịch sử) gỡ tham chiếu tới ca
    /// để FK không chặn. Gọi trong transaction của người gọi; trả về các đơn vừa tự hủy.
    /// </summary>
    private static async Task<List<LeaveRequest>> ReleaseAssignmentsAsync(
        NursingHomeContext context, List<int> assignmentIds, DateTime now, string note)
    {
        if (assignmentIds.Count == 0)
        {
            return [];
        }

        var linked = await context.LeaveRequests
            .Where(item => (item.ShiftAssignmentId != null && assignmentIds.Contains(item.ShiftAssignmentId.Value))
                || (item.SwapShiftAssignmentId != null && assignmentIds.Contains(item.SwapShiftAssignmentId.Value)))
            .ToListAsync();
        var cancelled = new List<LeaveRequest>();
        foreach (var item in linked)
        {
            if (item.Status == LeaveRequestStatus.Pending)
            {
                Review(item, LeaveRequestStatus.Cancelled, null, now, note);
                cancelled.Add(item);
            }

            if (item.ShiftAssignmentId is int mine && assignmentIds.Contains(mine))
            {
                item.ShiftAssignmentId = null;
            }

            if (item.SwapShiftAssignmentId is int theirs && assignmentIds.Contains(theirs))
            {
                item.SwapShiftAssignmentId = null;
            }
        }

        await context.SaveChangesAsync();
        return cancelled;
    }
}
