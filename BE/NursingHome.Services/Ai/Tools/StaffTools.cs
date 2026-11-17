using System.Text.Json;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Ai.Tools;

// ======================================================================================================
// BL-V4 — AI-05: trợ lý nhân viên. Mọi tool CHỈ ĐỌC và luôn dùng CurrentUser của người hỏi (việc của chính họ,
// ca của chính họ, bàn giao họ được nhận). Tài liệu nội bộ tra bằng search_knowledge(publicOnly = false).
// ======================================================================================================

public static class StaffTools
{
    public static IReadOnlyList<IAiTool> Create(IStaffHomeService home, IAttendanceService attendance, IHandoverService handovers, IAiRepository ai) =>
        [new MyTasksTodayTool(home), new MyNextShiftTool(attendance), new HandoverInboxTool(handovers), new SearchKnowledgeTool(ai, publicOnly: false)];
}

public sealed class MyTasksTodayTool(IStaffHomeService home) : IAiTool
{
    public AiToolDefinition Definition { get; } = new("my_tasks_today",
        "Danh sách việc chăm sóc được giao cho chính người đang hỏi: có hạn hôm nay hoặc đã quá hạn mà chưa xong.",
        AiToolJson.Schema());

    public async Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (user is null)
        {
            throw new NursingHome.BusinessObjects.Exceptions.ForbiddenException();
        }

        var tasks = await home.TasksTodayAsync(user);
        var open = tasks.Where(task => task.Status != "Completed").ToList();
        var overdue = open.Count(task => task.Overdue);
        string top = string.Join("; ", open.Take(5).Select(task => $"{task.DueAt + TimeSpan.FromHours(7):HH:mm} {task.ResidentCode} {task.Title}{(task.Overdue ? " (quá hạn)" : "")}"));
        return AiToolJson.Serialize(new
        {
            total = tasks.Count,
            done = tasks.Count - open.Count,
            open = open.Count,
            overdue,
            tasks = open.Take(15).Select(task => new { task.ResidentCode, task.ResidentName, task.Title, dueAt = task.DueAt, task.Status, task.Overdue }),
            summary = tasks.Count == 0
                ? "Hôm nay bạn không có việc chăm sóc nào được giao."
                : $"Hôm nay bạn có {tasks.Count} việc, đã xong {tasks.Count - open.Count}, còn {open.Count}" + (overdue > 0 ? $" ({overdue} quá hạn)" : "")
                    + (open.Count > 0 ? $": {top}." : ".")
        });
    }
}

public sealed class MyNextShiftTool(IAttendanceService attendance) : IAiTool
{
    public AiToolDefinition Definition { get; } = new("my_next_shift",
        "Ca trực kế tiếp (và ca đang làm nếu có) của chính người đang hỏi, trong 7 ngày tới.",
        AiToolJson.Schema());

    public async Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (user is null)
        {
            throw new NursingHome.BusinessObjects.Exceptions.ForbiddenException();
        }

        var shifts = await attendance.MyShiftsAsync(user);
        var current = shifts.FirstOrDefault(shift => shift.Status is "Open" or "InProgress");
        var next = shifts.FirstOrDefault(shift => shift.Status == "Upcoming");
        string Describe(NursingHome.DTOs.Staffing.MyShiftDto shift) =>
            $"{shift.ShiftName} ngày {shift.WorkDate:dd/MM} ({shift.StartsAt + TimeSpan.FromHours(7):HH:mm}–{shift.EndsAt + TimeSpan.FromHours(7):HH:mm})";
        return AiToolJson.Serialize(new
        {
            current = current is null ? null : new { current.ShiftName, current.WorkDate, current.StartsAt, current.EndsAt, current.Status },
            next = next is null ? null : new { next.ShiftName, next.WorkDate, next.StartsAt, next.EndsAt },
            summary = (current is null ? "" : $"Bạn đang trong ca {Describe(current)}. ")
                + (next is null ? "Bạn chưa có ca nào sắp tới trong 7 ngày." : $"Ca kế tiếp của bạn: {Describe(next)}.")
        });
    }
}

public sealed class HandoverInboxTool(IHandoverService handovers) : IAiTool
{
    public AiToolDefinition Definition { get; } = new("handover_inbox",
        "Các bản bàn giao ca gần đây mà người đang hỏi cần đọc (chưa xác nhận đã đọc).",
        AiToolJson.Schema());

    public async Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (user is null)
        {
            throw new NursingHome.BusinessObjects.Exceptions.ForbiddenException();
        }

        var inbox = await handovers.InboxAsync(user);
        return AiToolJson.Serialize(new
        {
            count = inbox.Count,
            handovers = inbox.Take(5).Select(item => new { item.ShiftName, item.WorkDate, item.FromAccountName, summary = item.Summary.Length <= 400 ? item.Summary : item.Summary[..400] + "…" }),
            summary = inbox.Count == 0
                ? "Bạn không có bản bàn giao nào chưa đọc."
                : $"Bạn có {inbox.Count} bản bàn giao chưa đọc. Gần nhất: ca {inbox[0].ShiftName} ngày {inbox[0].WorkDate:dd/MM} của {inbox[0].FromAccountName}: "
                    + (inbox[0].Summary.Length <= 300 ? inbox[0].Summary : inbox[0].Summary[..300] + "…")
        });
    }
}
