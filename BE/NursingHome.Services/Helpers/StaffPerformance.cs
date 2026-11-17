using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.DataAccessObjects;
using NursingHome.DTOs.Staffing;

namespace NursingHome.Services.Helpers;

/// <summary>
/// UC-126: hiệu suất nhân viên = % việc chăm sóc xong đúng hạn + chuyên cần (ca đã kết thúc có chấm công / đi muộn / vắng).
/// Chỉ ĐỌC dữ liệu việc chăm sóc và thuốc (bảng của Huy). "Đúng hạn" = hoàn thành trong hạn + 15 phút dung sai.
/// </summary>
public static class StaffPerformance
{
    public const int OnTimeGraceMinutes = 15;

    public static List<StaffPerformanceRowDto> Build(
        IEnumerable<Account> accounts,
        IEnumerable<TaskFact> tasks,
        IReadOnlyDictionary<int, int> medicationRecords,
        IEnumerable<AssignmentFact> assignments,
        DateTime now)
    {
        var tasksByAccount = tasks.Where(task => task.DueAt <= now).ToLookup(task => task.AssignedToId);
        var assignmentsByAccount = assignments
            .Where(item => StaffingRules.ShiftWindow(item.WorkDate, item.Shift).EndUtc <= now)
            .ToLookup(item => item.AccountId);

        var rows = new List<StaffPerformanceRowDto>();
        foreach (var account in accounts)
        {
            var mine = tasksByAccount[account.Id].ToList();
            int onTime = mine.Count(task => IsOnTime(task));
            int late = mine.Count(task => IsDone(task) && !IsOnTime(task));
            int overdueOpen = mine.Count(task => !IsDone(task) && task.Status is CareTaskStatus.Pending or CareTaskStatus.InProgress);

            var shifts = assignmentsByAccount[account.Id].ToList();
            int attended = shifts.Count(item => item.Attendance is not null);
            int lateCount = shifts.Count(item => item.Attendance is { LateMinutes: > 0 });

            var row = new StaffPerformanceRowDto
            {
                AccountId = account.Id,
                FullName = account.FullName,
                Role = account.Role,
                TasksDue = mine.Count,
                TasksOnTime = onTime,
                TasksLate = late,
                TasksOverdueOpen = overdueOpen,
                OnTimeRate = StaffHrRules.Percent(onTime, mine.Count),
                MedicationRecords = medicationRecords.GetValueOrDefault(account.Id),
                ShiftsAssigned = shifts.Count,
                ShiftsAttended = attended,
                LateCount = lateCount,
                Absent = shifts.Count - attended,
                AttendanceRate = StaffHrRules.Percent(attended, shifts.Count)
            };
            if (row.TasksDue > 0 || row.ShiftsAssigned > 0 || row.MedicationRecords > 0)
            {
                rows.Add(row);
            }
        }

        return rows.OrderBy(row => row.Role).ThenBy(row => row.FullName).ToList();
    }

    /// <summary>Đã xong: có lần ghi hoàn thành, hoặc trạng thái Completed (dữ liệu cũ không có bản ghi giờ).</summary>
    private static bool IsDone(TaskFact task) => task.CompletedAt is not null || task.Status == CareTaskStatus.Completed;

    private static bool IsOnTime(TaskFact task) =>
        IsDone(task) && (task.CompletedAt is null || task.CompletedAt <= task.DueAt.AddMinutes(OnTimeGraceMinutes));
}
