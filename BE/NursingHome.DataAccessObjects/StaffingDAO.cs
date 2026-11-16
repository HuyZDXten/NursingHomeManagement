using System.Data;
using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.DataAccessObjects;

/// <summary>
/// MODULE VẬN HÀNH (Viên) — S3-V2: danh mục ca, lịch ca, phân công phụ trách cư dân.
/// Nghỉ phép/đổi ca và bàn giao ca (S3-V4) ở StaffingDAO.Handover.cs.
/// </summary>
public sealed partial class StaffingDAO : GenericDAO<ShiftAssignment>
{
    public static StaffingDAO Instance { get; } = new();

    private StaffingDAO()
    {
    }

    // ---------------- Danh mục ca ----------------

    public async Task<List<Shift>> ListShiftsAsync(bool activeOnly)
    {
        await using var context = CreateContext();
        return await context.Shifts.AsNoTracking()
            .Where(shift => !activeOnly || shift.IsActive)
            .OrderBy(shift => shift.StartTime)
            .ToListAsync();
    }

    public async Task<Shift?> GetShiftAsync(int id)
    {
        await using var context = CreateContext();
        return await context.Shifts.AsNoTracking().FirstOrDefaultAsync(shift => shift.Id == id);
    }

    // ---------------- Lịch ca ----------------

    public async Task<List<ShiftAssignment>> ListAssignmentsAsync(DateOnly from, DateOnly to, int? accountId)
    {
        await using var context = CreateContext();
        return await context.ShiftAssignments.AsNoTracking()
            .Include(item => item.Account)
            .Include(item => item.Shift)
            .Where(item => item.WorkDate >= from && item.WorkDate <= to && (accountId == null || item.AccountId == accountId))
            .OrderBy(item => item.WorkDate).ThenBy(item => item.Shift.StartTime).ThenBy(item => item.Account.FullName)
            .ToListAsync();
    }

    public async Task<ShiftAssignment?> GetAssignmentAsync(int id)
    {
        await using var context = CreateContext();
        return await context.ShiftAssignments.AsNoTracking()
            .Include(item => item.Account)
            .Include(item => item.Shift)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task<bool> AssignmentExistsAsync(int accountId, DateOnly workDate, int shiftId)
    {
        await using var context = CreateContext();
        return await context.ShiftAssignments.AnyAsync(item =>
            item.AccountId == accountId && item.WorkDate == workDate && item.ShiftId == shiftId);
    }

    /// <summary>Trùng (AccountId, WorkDate, ShiftId) bị UQ_ShiftAssignments chặn → middleware trả 409.</summary>
    public Task<ShiftAssignment> AddAssignmentAsync(ShiftAssignment assignment) => AddAsync(assignment);

    /// <summary>Bỏ một ca; đơn đổi ca đang chờ có dính ca này tự hủy (trả về để báo người xin).</summary>
    public async Task<List<LeaveRequest>> DeleteAssignmentAsync(int id, DateTime now)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var assignment = await context.ShiftAssignments.FindAsync(id) ?? throw new NotFoundException("Không tìm thấy ca đã xếp.");
        var cancelled = await ReleaseAssignmentsAsync(context, [id], now, "Tự hủy: ca trong đơn đã bị bỏ khỏi lịch.");
        context.ShiftAssignments.Remove(assignment);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return cancelled;
    }

    /// <summary>
    /// Chép lịch tuần <paramref name="fromMonday"/> sang tuần <paramref name="toMonday"/> trong MỘT transaction:
    /// bỏ qua ô đã có, ngày trước <paramref name="earliest"/> và tài khoản đã khóa. Trả về các bản ghi đã thêm.
    /// </summary>
    public async Task<(List<ShiftAssignment> Added, int Skipped)> CopyWeekAsync(
        DateOnly fromMonday, DateOnly toMonday, DateOnly earliest, int byAccountId, DateTime now)
    {
        var offset = toMonday.DayNumber - fromMonday.DayNumber;
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var source = await context.ShiftAssignments.AsNoTracking()
            .Where(item => item.WorkDate >= fromMonday && item.WorkDate <= fromMonday.AddDays(6) && item.Account.IsActive)
            .ToListAsync();
        var existing = (await context.ShiftAssignments.AsNoTracking()
                .Where(item => item.WorkDate >= toMonday && item.WorkDate <= toMonday.AddDays(6))
                .Select(item => new { item.AccountId, item.WorkDate, item.ShiftId })
                .ToListAsync())
            .Select(item => (item.AccountId, item.WorkDate, item.ShiftId))
            .ToHashSet();

        var added = new List<ShiftAssignment>();
        foreach (var item in source)
        {
            var workDate = item.WorkDate.AddDays(offset);
            if (workDate < earliest || !existing.Add((item.AccountId, workDate, item.ShiftId)))
            {
                continue;
            }

            added.Add(new ShiftAssignment
            {
                AccountId = item.AccountId,
                ShiftId = item.ShiftId,
                WorkDate = workDate,
                Note = item.Note,
                CreatedById = byAccountId,
                CreatedAt = now
            });
        }

        context.ShiftAssignments.AddRange(added);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return (added, source.Count - added.Count);
    }

    // ---------------- Phân công phụ trách ----------------

    public async Task<List<ResidentCareAssignment>> ListCareAssignmentsAsync(int? residentId, int? accountId, bool activeOnly)
    {
        await using var context = CreateContext();
        return await context.ResidentCareAssignments.AsNoTracking()
            .Include(item => item.Resident)
            .Include(item => item.Account)
            .Include(item => item.AssignedBy)
            .Include(item => item.Shift)
            .Where(item => (residentId == null || item.ResidentId == residentId)
                && (accountId == null || item.AccountId == accountId)
                && (!activeOnly || item.EndDate == null))
            .OrderBy(item => item.Resident.Code).ThenBy(item => item.ShiftId).ThenByDescending(item => item.StartDate)
            .ToListAsync();
    }

    public async Task<ResidentCareAssignment?> GetCareAssignmentAsync(int id)
    {
        await using var context = CreateContext();
        return await context.ResidentCareAssignments.AsNoTracking()
            .Include(item => item.Resident)
            .Include(item => item.Account)
            .Include(item => item.AssignedBy)
            .Include(item => item.Shift)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    /// <summary>
    /// Một người phụ trách chính đang hiệu lực mỗi (cư dân, ca); "mọi ca" (ShiftId NULL) trùng với mọi ca cụ thể.
    /// Kiểm và ghi trong một transaction Serializable; UX_RCA_ActivePrimary là chốt chặn cuối ở DB.
    /// </summary>
    public async Task<ResidentCareAssignment> AddCareAssignmentAsync(ResidentCareAssignment assignment)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var clash = await context.ResidentCareAssignments.Include(item => item.Account)
            .Where(item => item.ResidentId == assignment.ResidentId && item.EndDate == null
                && (assignment.ShiftId == null || item.ShiftId == null || item.ShiftId == assignment.ShiftId))
            .FirstOrDefaultAsync();
        if (clash is not null)
        {
            throw new ConflictException($"Cư dân đã có người phụ trách chính cho ca này ({clash.Account.FullName}). Kết thúc phân công cũ trước.");
        }

        context.ResidentCareAssignments.Add(assignment);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return assignment;
    }

    public async Task EndCareAssignmentAsync(int id, DateOnly endDate)
    {
        await using var context = CreateContext();
        var assignment = await context.ResidentCareAssignments.FindAsync(id)
            ?? throw new NotFoundException("Không tìm thấy phân công phụ trách.");
        if (assignment.EndDate is not null)
        {
            throw new BusinessRuleException("Phân công này đã kết thúc.");
        }

        assignment.EndDate = endDate < assignment.StartDate ? assignment.StartDate : endDate;
        await context.SaveChangesAsync();
    }

    /// <summary>Đang là người phụ trách chính (phân công đã bắt đầu và chưa kết thúc) — dùng cho quyền xem hồ sơ.</summary>
    public async Task<bool> IsPrimaryCaregiverAsync(int residentId, int accountId, DateOnly today)
    {
        await using var context = CreateContext();
        return await context.ResidentCareAssignments.AnyAsync(item =>
            item.ResidentId == residentId && item.AccountId == accountId && item.EndDate == null && item.StartDate <= today);
    }

    public async Task<List<int>> PrimaryResidentIdsAsync(int accountId, DateOnly today)
    {
        await using var context = CreateContext();
        return await context.ResidentCareAssignments
            .Where(item => item.AccountId == accountId && item.EndDate == null && item.StartDate <= today)
            .Select(item => item.ResidentId)
            .Distinct()
            .ToListAsync();
    }
}
