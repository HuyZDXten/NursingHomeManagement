using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.DataAccessObjects;

/// <summary>Một việc chăm sóc có hạn trong khoảng báo cáo; CompletedAt = lúc ghi nhận hoàn thành đầu tiên (null = chưa xong).</summary>
public sealed record TaskFact(int AssignedToId, DateTime DueAt, CareTaskStatus Status, DateTime? CompletedAt);

/// <summary>Một phân ca kèm dòng chấm công (nếu có) để tính chuyên cần.</summary>
public sealed record AssignmentFact(int AssignmentId, int AccountId, DateOnly WorkDate, Shift Shift, Attendance? Attendance);

/// <summary>MODULE VẬN HÀNH (Viên) — BL-V4: hồ sơ nhân viên & chứng chỉ (UC-43), chấm công (UC-48), dữ liệu hiệu suất (UC-126).</summary>
public sealed class StaffHrDAO : GenericDAO<StaffProfile>
{
    public static StaffHrDAO Instance { get; } = new();

    private StaffHrDAO()
    {
    }

    // ---------------- Hồ sơ ----------------

    public async Task<List<Account>> ListStaffAccountsAsync(IReadOnlyCollection<string> roles)
    {
        await using var context = CreateContext();
        return await context.Accounts.AsNoTracking().Where(item => roles.Contains(item.Role))
            .OrderBy(item => item.Role).ThenBy(item => item.FullName).ToListAsync();
    }

    public async Task<List<StaffProfile>> ListProfilesAsync()
    {
        await using var context = CreateContext();
        return await context.StaffProfiles.AsNoTracking().ToListAsync();
    }

    public async Task<StaffProfile?> GetProfileAsync(int accountId)
    {
        await using var context = CreateContext();
        return await context.StaffProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.AccountId == accountId);
    }

    /// <summary>Thêm hoặc cập nhật; hai request tạo cùng lúc → bên thua đọc lại rồi cập nhật.</summary>
    public async Task UpsertProfileAsync(StaffProfile profile)
    {
        try
        {
            await using var context = CreateContext();
            var existing = await context.StaffProfiles.FirstOrDefaultAsync(item => item.AccountId == profile.AccountId);
            if (existing is null)
            {
                context.StaffProfiles.Add(profile);
            }
            else
            {
                Copy(profile, existing);
            }

            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await using var retry = CreateContext();
            var existing = await retry.StaffProfiles.FirstAsync(item => item.AccountId == profile.AccountId);
            Copy(profile, existing);
            await retry.SaveChangesAsync();
        }
    }

    private static void Copy(StaffProfile from, StaffProfile to)
    {
        to.Position = from.Position;
        to.Department = from.Department;
        to.HireDate = from.HireDate;
        to.IdNumber = from.IdNumber;
        to.Address = from.Address;
        to.EmergencyContactName = from.EmergencyContactName;
        to.EmergencyContactPhone = from.EmergencyContactPhone;
        to.Notes = from.Notes;
        to.UpdatedAt = from.UpdatedAt;
    }

    // ---------------- Chứng chỉ ----------------

    public async Task<List<StaffCertificate>> ListCertificatesAsync(int? accountId)
    {
        await using var context = CreateContext();
        return await context.StaffCertificates.AsNoTracking().Include(item => item.Account)
            .Where(item => accountId == null || item.AccountId == accountId)
            .OrderBy(item => item.ExpiresOn == null).ThenBy(item => item.ExpiresOn).ThenBy(item => item.Name).ToListAsync();
    }

    public async Task<StaffCertificate?> GetCertificateAsync(int id)
    {
        await using var context = CreateContext();
        return await context.StaffCertificates.AsNoTracking().Include(item => item.Account).FirstOrDefaultAsync(item => item.Id == id);
    }

    public Task<StaffCertificate> AddCertificateAsync(StaffCertificate certificate) => AddEntityAsync(certificate);

    /// <summary>Đổi ngày hết hạn thì xóa dấu "đã báo" để mốc mới được báo lại.</summary>
    public async Task<bool> UpdateCertificateAsync(int id, Action<StaffCertificate> apply)
    {
        await using var context = CreateContext();
        var certificate = await context.StaffCertificates.FirstOrDefaultAsync(item => item.Id == id);
        if (certificate is null)
        {
            return false;
        }

        var oldExpiry = certificate.ExpiresOn;
        apply(certificate);
        if (certificate.ExpiresOn != oldExpiry)
        {
            certificate.Alert30At = null;
            certificate.AlertExpiredAt = null;
        }

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteCertificateAsync(int id)
    {
        await using var context = CreateContext();
        return await context.StaffCertificates.Where(item => item.Id == id).ExecuteDeleteAsync() == 1;
    }

    /// <summary>Chứng chỉ của nhân viên còn hoạt động có hạn ≤ <paramref name="until"/> (kể cả đã quá hạn).</summary>
    public async Task<List<StaffCertificate>> ListExpiringAsync(DateOnly until)
    {
        await using var context = CreateContext();
        return await context.StaffCertificates.AsNoTracking().Include(item => item.Account)
            .Where(item => item.ExpiresOn != null && item.ExpiresOn <= until && item.Account.IsActive)
            .OrderBy(item => item.ExpiresOn).ToListAsync();
    }

    /// <summary>Đánh dấu đã báo một mốc; trả false nếu mốc đó đã được báo (một instance khác vừa báo).</summary>
    public async Task<bool> MarkAlertedAsync(int id, bool expiredStage, DateTime now)
    {
        await using var context = CreateContext();
        var query = context.StaffCertificates.Where(item => item.Id == id);
        int changed = expiredStage
            ? await query.Where(item => item.AlertExpiredAt == null).ExecuteUpdateAsync(set => set.SetProperty(item => item.AlertExpiredAt, now))
            : await query.Where(item => item.Alert30At == null).ExecuteUpdateAsync(set => set.SetProperty(item => item.Alert30At, now));
        return changed == 1;
    }

    // ---------------- Chấm công ----------------

    public async Task<ShiftAssignment?> GetAssignmentAsync(int id)
    {
        await using var context = CreateContext();
        return await context.ShiftAssignments.AsNoTracking().Include(item => item.Shift).Include(item => item.Account)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task<Attendance?> GetAttendanceAsync(int assignmentId)
    {
        await using var context = CreateContext();
        return await context.Attendances.AsNoTracking().FirstOrDefaultAsync(item => item.ShiftAssignmentId == assignmentId);
    }

    /// <summary>Phân ca của một người trong khoảng ngày, kèm chấm công.</summary>
    public async Task<List<AssignmentFact>> ListAssignmentFactsAsync(int? accountId, DateOnly from, DateOnly to)
    {
        await using var context = CreateContext();
        var assignments = await context.ShiftAssignments.AsNoTracking().Include(item => item.Shift).Include(item => item.Account)
            .Where(item => item.WorkDate >= from && item.WorkDate <= to && (accountId == null || item.AccountId == accountId))
            .OrderBy(item => item.WorkDate).ThenBy(item => item.Shift.StartTime).ToListAsync();
        var ids = assignments.Select(item => item.Id).ToList();
        var attendance = (await context.Attendances.AsNoTracking().Where(item => ids.Contains(item.ShiftAssignmentId)).ToListAsync())
            .ToDictionary(item => item.ShiftAssignmentId);
        return assignments.Select(item => new AssignmentFact(item.Id, item.AccountId, item.WorkDate, item.Shift,
            attendance.GetValueOrDefault(item.Id))).ToList();
    }

    /// <summary>Vào ca: mỗi phân ca một dòng (UQ_Attendances_Assignment) nên hai lần bấm cùng lúc chỉ ghi một lần.</summary>
    public async Task<Attendance> CheckInAsync(Attendance attendance)
    {
        try
        {
            return await AddEntityAsync(attendance);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("Bạn đã vào ca này rồi.");
        }
    }

    public async Task<bool> CheckOutAsync(int assignmentId, int accountId, DateTime now)
    {
        await using var context = CreateContext();
        int changed = await context.Attendances
            .Where(item => item.ShiftAssignmentId == assignmentId && item.AccountId == accountId && item.CheckOutAt == null && item.CheckInAt <= now)
            .ExecuteUpdateAsync(set => set.SetProperty(item => item.CheckOutAt, now));
        return changed == 1;
    }

    /// <summary>Admin sửa hoặc tạo công (ví dụ quên chấm công): luôn ghi người sửa, giờ sửa, lý do.</summary>
    public async Task<Attendance> UpsertEditAsync(int assignmentId, int accountId, DateTime checkInAt, DateTime? checkOutAt,
        int lateMinutes, string reason, int editorId, DateTime now)
    {
        await using var context = CreateContext();
        var existing = await context.Attendances.FirstOrDefaultAsync(item => item.ShiftAssignmentId == assignmentId);
        if (existing is null)
        {
            existing = new Attendance { ShiftAssignmentId = assignmentId, AccountId = accountId };
            context.Attendances.Add(existing);
        }

        existing.CheckInAt = checkInAt;
        existing.CheckOutAt = checkOutAt;
        existing.LateMinutes = lateMinutes;
        existing.EditedById = editorId;
        existing.EditedAt = now;
        existing.EditReason = reason;
        await context.SaveChangesAsync();
        return existing;
    }

    // ---------------- Dữ liệu hiệu suất (chỉ đọc bảng của Huy) ----------------

    public async Task<List<TaskFact>> TaskFactsAsync(DateTime fromUtc, DateTime toUtc)
    {
        await using var context = CreateContext();
        var tasks = await context.CareTasks.AsNoTracking()
            .Where(item => item.DueAt >= fromUtc && item.DueAt < toUtc && item.Status != CareTaskStatus.Cancelled)
            .Select(item => new { item.Id, item.AssignedToId, item.DueAt, item.Status })
            .ToListAsync();
        var completed = (await context.CareRecords.AsNoTracking()
            .Where(item => item.Status == CareTaskStatus.Completed && item.CareTask.DueAt >= fromUtc && item.CareTask.DueAt < toUtc)
            .GroupBy(item => item.CareTaskId)
            .Select(group => new { TaskId = group.Key, At = group.Min(item => item.PerformedAt) })
            .ToListAsync()).ToDictionary(item => item.TaskId, item => item.At);
        return tasks.Select(item => new TaskFact(item.AssignedToId, item.DueAt, item.Status,
            completed.TryGetValue(item.Id, out var at) ? at : null)).ToList();
    }

    /// <summary>Số lần ghi nhận thuốc (đã dùng/bỏ lỡ/từ chối) theo người ghi trong khoảng.</summary>
    public async Task<Dictionary<int, int>> MedicationRecordCountsAsync(DateTime fromUtc, DateTime toUtc)
    {
        await using var context = CreateContext();
        return await context.MedicationLogs.AsNoTracking()
            .Where(item => item.RecordedById != null && item.RecordedAt != null && item.RecordedAt >= fromUtc && item.RecordedAt < toUtc)
            .GroupBy(item => item.RecordedById!.Value)
            .Select(group => new { AccountId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.AccountId, item => item.Count);
    }

    // ---------------- Trang chủ cá nhân ----------------

    /// <summary>Việc của tôi tới hết hôm nay: có hạn hôm nay, hoặc quá hạn từ trước mà chưa xong (không tính việc đã hủy).</summary>
    public async Task<List<CareTask>> TasksForTodayAsync(int accountId, DateTime startOfDayUtc, DateTime endOfDayUtc)
    {
        await using var context = CreateContext();
        return await context.CareTasks.AsNoTracking().Include(item => item.Resident)
            .Where(item => item.AssignedToId == accountId && item.Status != CareTaskStatus.Cancelled && item.DueAt < endOfDayUtc
                && (item.DueAt >= startOfDayUtc || item.Status == CareTaskStatus.Pending || item.Status == CareTaskStatus.InProgress))
            .OrderBy(item => item.DueAt).Take(100).ToListAsync();
    }
}
