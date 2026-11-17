using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Common;
using NursingHome.DTOs.Staffing;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>
/// BL-V4, UC-127: trang chủ cá nhân của nhân viên: việc hôm nay, ca đang làm/kế tiếp, bàn giao và tin nhắn chưa đọc,
/// chứng chỉ của chính mình sắp hết hạn. Việc chăm sóc chỉ ĐỌC (bảng của module Chăm sóc).
/// </summary>
public class StaffHomeService(
    IStaffHrRepository hr,
    IAttendanceService attendance,
    IHandoverService handovers,
    IChatService chat,
    IStaffHrService staffHr,
    FacilityClock clock) : IStaffHomeService
{
    public async Task<StaffHomeDto> GetAsync(CurrentUser user)
    {
        var now = clock.UtcNow;
        var tasks = await TasksTodayAsync(user);
        var shifts = await attendance.MyShiftsAsync(user);
        var certificates = user.IsInRole(Roles.Family) ? [] : (await staffHr.GetProfileAsync(user, user.AccountId)).Certificates
            .Where(item => item.State != CertificateState.Valid.ToString()).ToList();

        return new StaffHomeDto
        {
            Tasks = tasks,
            TasksDone = tasks.Count(task => task.Status == CareTaskStatus.Completed.ToString()),
            TasksOpen = tasks.Count(task => task.Status != CareTaskStatus.Completed.ToString()),
            TasksOverdue = tasks.Count(task => task.Overdue),
            CurrentShift = shifts.FirstOrDefault(shift => shift.CanCheckIn || shift.CanCheckOut),
            NextShift = shifts.FirstOrDefault(shift => shift.Status == "Upcoming" && shift.StartsAt > now),
            UnreadHandovers = await CountAsync(async () => (await handovers.InboxAsync(user)).Count),
            UnreadMessages = await CountAsync(async () => (await chat.ListAsync(user)).Sum(item => item.Unread)),
            Certificates = certificates
        };
    }

    public async Task<List<HomeTaskDto>> TasksTodayAsync(CurrentUser user)
    {
        var today = clock.Today;
        var now = clock.UtcNow;
        var tasks = await hr.TasksForTodayAsync(user.AccountId, clock.StartOfDayUtc(today), clock.StartOfDayUtc(today.AddDays(1)));
        return tasks.Select(task => new HomeTaskDto
        {
            Id = task.Id,
            ResidentId = task.ResidentId,
            ResidentCode = task.Resident.Code,
            ResidentName = task.Resident.FullName,
            Title = task.Title,
            DueAt = task.DueAt,
            Status = task.Status.ToString(),
            Overdue = task.DueAt < now && task.Status is CareTaskStatus.Pending or CareTaskStatus.InProgress
        }).ToList();
    }

    /// <summary>Vai trò không dùng tính năng (ví dụ Kế toán không có chat) thì đếm = 0, không làm hỏng cả trang.</summary>
    private static async Task<int> CountAsync(Func<Task<int>> count)
    {
        try
        {
            return await count();
        }
        catch (ForbiddenException)
        {
            return 0;
        }
    }
}
