using Microsoft.Extensions.Options;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Care;
using NursingHome.DTOs.Common;
using NursingHome.DTOs.Staffing;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.Services.Implementations;

/// <summary>
/// S3-V4 — UC-47 nghỉ phép/đổi ca (nhân viên xin, Admin duyệt), UC-49 bàn giao ca (gợi ý từ dữ liệu trong ca,
/// người ca kế tiếp xác nhận đã đọc). Bàn giao là đầu vào của AI tóm tắt ca (S4-V2).
/// </summary>
public class HandoverService(
    IStaffingRepository staffing,
    IAccountRepository accounts,
    IResidentAccessService access,
    INotificationService notifications,
    IOptions<FacilityOptions> facility,
    FacilityClock clock) : IHandoverService
{
    public const int MaxLeaveDays = 31;
    private const int SwapHorizonDays = 28;
    private const string LeaveLink = "/staff/shifts?tab=leave";
    private const string HandoverLink = "/staff/shifts?tab=handover";

    private static readonly string[] ShiftRoles = [Roles.Doctor, Roles.Nurse, Roles.Caregiver];

    // ---------------- Đơn nghỉ phép / đổi ca ----------------

    public async Task<List<LeaveRequestDto>> ListLeaveRequestsAsync(CurrentUser user, string? status)
    {
        LeaveRequestStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            filter = Enum.TryParse<LeaveRequestStatus>(status, ignoreCase: true, out var parsed)
                ? parsed
                : throw new BusinessRuleException("Trạng thái đơn không hợp lệ.");
        }

        int? involving = user.IsInRole(Roles.Admin) ? null : user.AccountId;
        return (await staffing.ListLeaveRequestsAsync(involving, filter, take: 200)).Select(item => item.ToDto()).ToList();
    }

    public async Task<LeaveRequestDto> CreateLeaveRequestAsync(CurrentUser user, CreateLeaveRequest request)
    {
        if (!user.IsInRole(ShiftRoles))
        {
            throw new BusinessRuleException("Chỉ bác sĩ, điều dưỡng và hộ lý có lịch ca mới tạo đơn.");
        }

        var created = request.Type == nameof(LeaveRequestType.Swap)
            ? await CreateSwapAsync(user, request)
            : await CreateLeaveAsync(user, request);
        return await GetLeaveDtoAsync(created.Id);
    }

    /// <summary>
    /// Ca của đồng nghiệp cùng role (4 tuần tới, chưa bắt đầu) đổi được lấy <paramref name="shiftAssignmentId"/>:
    /// bỏ ô giống hệt và ô mà sau khi đổi một trong hai người bị trùng ca.
    /// </summary>
    public async Task<List<ShiftAssignmentDto>> SwapCandidatesAsync(CurrentUser user, int shiftAssignmentId)
    {
        var mine = await staffing.GetAssignmentAsync(shiftAssignmentId) ?? throw new NotFoundException("Không tìm thấy ca của bạn.");
        if (mine.AccountId != user.AccountId)
        {
            throw new ForbiddenException("Chỉ đổi được ca của chính mình.");
        }

        var now = clock.UtcNow;
        var today = clock.Today;
        var all = await staffing.ListAssignmentsAsync(today, today.AddDays(SwapHorizonDays - 1), null);
        var cells = all.Select(item => (item.AccountId, item.WorkDate, item.ShiftId)).ToHashSet();
        return all
            .Where(item => item.AccountId != user.AccountId && item.Account.Role == user.Role && item.Account.IsActive
                && !HasStarted(item, now)
                && !(item.WorkDate == mine.WorkDate && item.ShiftId == mine.ShiftId)
                && !cells.Contains((user.AccountId, item.WorkDate, item.ShiftId))
                && !cells.Contains((item.AccountId, mine.WorkDate, mine.ShiftId)))
            .Select(item => item.ToDto())
            .ToList();
    }

    public async Task<LeaveApprovalDto> ApproveAsync(CurrentUser user, int id, string? note)
    {
        var request = await staffing.GetLeaveRequestAsync(id) ?? throw new NotFoundException("Không tìm thấy đơn.");
        var reviewNote = Clean(note);
        var now = clock.UtcNow;
        var vacated = new List<LeaveShiftDto>();
        List<LeaveRequest> cancelled;

        if (request.Type == LeaveRequestType.Swap)
        {
            if (request.ShiftAssignment is { } mine && HasStarted(mine, now)
                || request.SwapShiftAssignment is { } theirs && HasStarted(theirs, now))
            {
                throw new BusinessRuleException("Ca trong đơn đã bắt đầu, không đổi được nữa. Hãy từ chối đơn này.");
            }

            cancelled = await staffing.ApproveSwapAsync(id, user.AccountId, now, reviewNote);
            await notifications.NotifyAsync(Involved(request), NotificationType.System, "Đơn đổi ca được duyệt",
                $"{request.Account.FullName} làm {Describe(request.SwapShiftAssignment)}, {request.SwapWithAccount?.FullName} làm {Describe(request.ShiftAssignment)}.",
                "/staff/shifts");
        }
        else
        {
            var (removed, released) = await staffing.ApproveLeaveAsync(id, user.AccountId, now, reviewNote,
                assignment => !HasStarted(assignment, now));
            cancelled = released;
            vacated = removed.OrderBy(item => item.WorkDate).ThenBy(item => item.Shift.StartTime).Select(item => item.ToLeaveShift()).ToList();
            await notifications.NotifyAsync([request.AccountId], NotificationType.System, "Đơn nghỉ phép được duyệt",
                $"Nghỉ {request.FromDate:dd/MM} – {request.ToDate:dd/MM/yyyy}"
                + (vacated.Count > 0 ? $", đã bỏ {vacated.Count} ca khỏi lịch." : "."), LeaveLink);
            if (vacated.Count > 0)
            {
                await notifications.NotifyRolesAsync([Roles.Admin], NotificationType.System,
                    $"Cần xếp người thay cho {request.Account.FullName}",
                    string.Join(", ", vacated.Select(item => $"{item.ShiftName.ToLowerInvariant()} {item.WorkDate:dd/MM}")) + ".",
                    "/staff/shifts");
            }
        }

        await NotifyAutoCancelledAsync(cancelled);
        return new LeaveApprovalDto { Request = await GetLeaveDtoAsync(id), VacatedShifts = vacated };
    }

    public async Task<LeaveRequestDto> RejectAsync(CurrentUser user, int id, string? note)
    {
        var request = await staffing.GetLeaveRequestAsync(id) ?? throw new NotFoundException("Không tìm thấy đơn.");
        var reviewNote = Clean(note);
        await staffing.SetLeaveStatusIfPendingAsync(id, LeaveRequestStatus.Rejected, user.AccountId, clock.UtcNow, reviewNote);
        await notifications.NotifyAsync(Involved(request), NotificationType.System,
            request.Type == LeaveRequestType.Swap ? "Đơn đổi ca bị từ chối" : "Đơn nghỉ phép bị từ chối",
            reviewNote ?? "Liên hệ Admin để biết thêm.", LeaveLink);
        return await GetLeaveDtoAsync(id);
    }

    public async Task<LeaveRequestDto> CancelAsync(CurrentUser user, int id)
    {
        var request = await staffing.GetLeaveRequestAsync(id) ?? throw new NotFoundException("Không tìm thấy đơn.");
        if (request.AccountId != user.AccountId)
        {
            throw new ForbiddenException("Chỉ người tạo đơn được hủy đơn.");
        }

        await staffing.SetLeaveStatusIfPendingAsync(id, LeaveRequestStatus.Cancelled, null, clock.UtcNow, "Người xin tự hủy.");
        if (request.SwapWithAccountId is int colleague)
        {
            await notifications.NotifyAsync([colleague], NotificationType.System, "Đơn đổi ca đã hủy",
                $"{request.Account.FullName} đã hủy đơn xin đổi ca với bạn.", LeaveLink);
        }

        return await GetLeaveDtoAsync(id);
    }

    // ---------------- Bàn giao ca ----------------

    public async Task<HandoverDraftDto> GetDraftAsync(CurrentUser user, int shiftId, DateOnly? date)
    {
        var shift = await RequireShiftAsync(shiftId);
        var workDate = date ?? clock.Today;
        var (startUtc, endUtc) = StaffingRules.ShiftWindow(workDate, shift);
        var data = await staffing.GetHandoverDraftDataAsync(startUtc, endUtc, await access.VisibleResidentIdsAsync(user));
        var limits = facility.Value.VitalAlerts;

        var items = new List<(DateTime At, HandoverItemDto Item)>();
        items.AddRange(data.OpenTasks.Select(task => (task.DueAt, new HandoverItemDto
        {
            Kind = "Task",
            ResidentId = task.ResidentId,
            ResidentName = $"{task.Resident.Code} · {task.Resident.FullName}",
            Text = $"Chưa xong: {task.Title} (hạn {Local(task.DueAt)}, {(task.Status == CareTaskStatus.InProgress ? "đang làm" : "chưa làm")}, {task.AssignedTo.FullName})."
        })));
        items.AddRange(data.MissedMedications.Select(log => (log.ScheduledAt, new HandoverItemDto
        {
            Kind = "Medication",
            ResidentId = log.MedicationSchedule.ResidentId,
            ResidentName = $"{log.MedicationSchedule.Resident.Code} · {log.MedicationSchedule.Resident.FullName}",
            Text = $"Thuốc {log.MedicationSchedule.MedicationName} {log.MedicationSchedule.Dose} lúc {Local(log.ScheduledAt)} bị "
                + (log.Status == MedicationStatus.Refused ? "từ chối" : "bỏ lỡ")
                + (string.IsNullOrWhiteSpace(log.Notes) ? "." : $": {log.Notes.Trim()}")
        })));
        foreach (var vital in data.Vitals)
        {
            var alerts = HealthService.OutOfRange(new RecordVitalSignRequest
            {
                ResidentId = vital.ResidentId,
                Systolic = vital.Systolic,
                Diastolic = vital.Diastolic,
                HeartRate = vital.HeartRate,
                Temperature = vital.Temperature,
                SpO2 = vital.SpO2
            }, limits);
            if (alerts.Count == 0)
            {
                continue;
            }

            data.Residents.TryGetValue(vital.ResidentId, out var resident);
            items.Add((vital.RecordedAt, new HandoverItemDto
            {
                Kind = "Vital",
                ResidentId = vital.ResidentId,
                ResidentName = resident.Code is null ? null : $"{resident.Code} · {resident.FullName}",
                Text = $"Sinh hiệu cần theo dõi lúc {Local(vital.RecordedAt)}: {string.Join("; ", alerts)}."
            }));
        }

        // Sự cố trong ca (Kind = "Incident") thêm khi bảng Incidents của S3-H1 đã merge.

        return new HandoverDraftDto
        {
            ShiftId = shift.Id,
            ShiftName = shift.Name,
            WorkDate = workDate,
            StartsAt = startUtc,
            EndsAt = endUtc,
            Items = items.OrderBy(item => item.At).Select(item => item.Item).ToList()
        };
    }

    public async Task<HandoverDto> CreateHandoverAsync(CurrentUser user, CreateHandoverRequest request)
    {
        var shift = await RequireShiftAsync(request.ShiftId);
        var (startUtc, _) = StaffingRules.ShiftWindow(request.WorkDate, shift);
        if (startUtc > clock.UtcNow)
        {
            throw new BusinessRuleException("Ca chưa bắt đầu, chưa viết bàn giao được.");
        }

        if (!user.IsInRole(Roles.Admin) && !await staffing.AssignmentExistsAsync(user.AccountId, request.WorkDate, request.ShiftId))
        {
            throw new ForbiddenException($"Bạn không có {shift.Name.ToLowerInvariant()} ngày {request.WorkDate:dd/MM/yyyy} nên không viết bàn giao cho ca này.");
        }

        if (await staffing.HandoverExistsAsync(request.WorkDate, request.ShiftId, user.AccountId))
        {
            throw new ConflictException("Bạn đã viết bàn giao cho ca này.");
        }

        var highlights = request.Highlights
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .Select(item => new HandoverItemDto
            {
                Kind = item.Kind.Trim(),
                ResidentId = item.ResidentId,
                ResidentName = item.ResidentName?.Trim(),
                Text = item.Text.Trim()
            })
            .ToList();
        var created = await staffing.AddHandoverAsync(new ShiftHandover
        {
            ShiftId = request.ShiftId,
            WorkDate = request.WorkDate,
            FromAccountId = user.AccountId,
            Summary = request.Summary.Trim(),
            HighlightsJson = OperationsMapper.WriteHighlights(highlights),
            AiGenerated = request.AiGenerated,
            CreatedAt = clock.UtcNow
        });

        var shifts = await staffing.ListShiftsAsync(activeOnly: true);
        var next = StaffingRules.NextShift(request.WorkDate, shift, shifts);
        if (next is { } upcoming)
        {
            var receivers = (await staffing.ListAssignmentsAsync(upcoming.WorkDate, upcoming.WorkDate, null))
                .Where(item => item.ShiftId == upcoming.Shift.Id && item.AccountId != user.AccountId)
                .Select(item => item.AccountId)
                .Distinct()
                .ToList();
            if (receivers.Count > 0)
            {
                await notifications.NotifyAsync(receivers, NotificationType.Care, $"Bàn giao {shift.Name.ToLowerInvariant()} {request.WorkDate:dd/MM}",
                    $"{user.Name} vừa bàn giao ca. Đọc và bấm \"Đã đọc\" khi nhận ca.", HandoverLink);
            }
        }

        return await GetHandoverDtoAsync(user, created.Id);
    }

    public async Task<List<HandoverDto>> ListHandoversAsync(CurrentUser user, DateOnly? date)
    {
        var day = date ?? clock.Today;
        var handovers = await staffing.ListHandoversAsync(day, day, unreadOnly: false);
        var context = await LoadAckContextAsync(day, day);
        return handovers
            .Select(item => item.ToDto(CanAcknowledge(user, item, context)))
            .Where(dto => CanSee(user, dto))
            .ToList();
    }

    /// <summary>Bàn giao chưa đọc 2 ngày gần nhất mà người xem được xác nhận (badge ở menu "Ca trực").</summary>
    public async Task<List<HandoverDto>> InboxAsync(CurrentUser user)
    {
        var to = clock.Today;
        var from = to.AddDays(-2);
        var handovers = await staffing.ListHandoversAsync(from, to, unreadOnly: true);
        var context = await LoadAckContextAsync(from, to);
        return handovers
            .Where(item => CanAcknowledge(user, item, context))
            .Select(item => item.ToDto(canAcknowledge: true))
            .ToList();
    }

    public async Task<HandoverDto> AcknowledgeAsync(CurrentUser user, long id)
    {
        var handover = await staffing.GetHandoverAsync(id) ?? throw new NotFoundException("Không tìm thấy bàn giao.");
        if (handover.AcknowledgedAt is not null)
        {
            throw new BusinessRuleException($"Bàn giao đã được {handover.AcknowledgedBy?.FullName} xác nhận.");
        }

        if (handover.FromAccountId == user.AccountId)
        {
            throw new BusinessRuleException("Không tự xác nhận bàn giao của mình.");
        }

        var context = await LoadAckContextAsync(handover.WorkDate, handover.WorkDate);
        if (!CanAcknowledge(user, handover, context))
        {
            throw new ForbiddenException("Chỉ người làm ca kế tiếp, điều dưỡng hoặc Admin xác nhận bàn giao.");
        }

        if (!await staffing.AcknowledgeHandoverAsync(id, user.AccountId, clock.UtcNow))
        {
            throw new BusinessRuleException("Bàn giao vừa được người khác xác nhận.");
        }

        await notifications.NotifyAsync([handover.FromAccountId], NotificationType.Care, "Bàn giao ca đã được đọc",
            $"{user.Name} đã xác nhận bàn giao {handover.Shift.Name.ToLowerInvariant()} {handover.WorkDate:dd/MM}.", HandoverLink);
        return await GetHandoverDtoAsync(user, id);
    }

    // ---------------- Quy tắc thuần (có unit test) ----------------

    /// <summary>Ca đã bắt đầu (hoặc đã qua) thì không đổi/bỏ qua đơn được nữa.</summary>
    public static bool HasStarted(ShiftAssignment assignment, DateTime nowUtc) =>
        StaffingRules.ShiftWindow(assignment.WorkDate, assignment.Shift).StartUtc <= nowUtc;

    /// <summary>Kiểm khoảng nghỉ: từ hôm nay trở đi, tối đa <see cref="MaxLeaveDays"/> ngày.</summary>
    public static string? ValidateLeaveRange(DateOnly from, DateOnly to, DateOnly today)
    {
        if (from < today)
        {
            return "Không xin nghỉ cho ngày đã qua.";
        }

        if (to < from)
        {
            return "Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.";
        }

        return to.DayNumber - from.DayNumber + 1 > MaxLeaveDays ? $"Mỗi đơn nghỉ tối đa {MaxLeaveDays} ngày." : null;
    }

    // ---------------- Nội bộ ----------------

    private async Task<LeaveRequest> CreateLeaveAsync(CurrentUser user, CreateLeaveRequest request)
    {
        if (request.FromDate is not DateOnly from || request.ToDate is not DateOnly to)
        {
            throw new BusinessRuleException("Chọn ngày bắt đầu và kết thúc nghỉ.");
        }

        if (ValidateLeaveRange(from, to, clock.Today) is { } error)
        {
            throw new BusinessRuleException(error);
        }

        if (await staffing.HasPendingLeaveOverlapAsync(user.AccountId, from, to))
        {
            throw new ConflictException("Bạn đã có đơn nghỉ chờ duyệt trùng khoảng ngày này.");
        }

        var created = await staffing.AddLeaveRequestAsync(new LeaveRequest
        {
            AccountId = user.AccountId,
            Type = LeaveRequestType.Leave,
            FromDate = from,
            ToDate = to,
            Reason = request.Reason.Trim(),
            CreatedAt = clock.UtcNow
        });
        await notifications.NotifyRolesAsync([Roles.Admin], NotificationType.System, $"Đơn nghỉ phép mới: {user.Name}",
            $"Nghỉ {from:dd/MM} – {to:dd/MM/yyyy}. Lý do: {created.Reason}", LeaveLink);
        return created;
    }

    private async Task<LeaveRequest> CreateSwapAsync(CurrentUser user, CreateLeaveRequest request)
    {
        if (request.ShiftAssignmentId is not int myId || request.SwapShiftAssignmentId is not int theirId)
        {
            throw new BusinessRuleException("Chọn ca của bạn và ca của đồng nghiệp muốn đổi.");
        }

        var mine = await staffing.GetAssignmentAsync(myId) ?? throw new NotFoundException("Không tìm thấy ca của bạn.");
        var theirs = await staffing.GetAssignmentAsync(theirId) ?? throw new NotFoundException("Không tìm thấy ca của đồng nghiệp.");
        if (mine.AccountId != user.AccountId)
        {
            throw new ForbiddenException("Chỉ đổi được ca của chính mình.");
        }

        if (theirs.AccountId == user.AccountId)
        {
            throw new BusinessRuleException("Chọn ca của một đồng nghiệp khác.");
        }

        if (theirs.Account.Role != user.Role || !await accounts.IsActiveInRolesAsync(theirs.AccountId, [user.Role]))
        {
            throw new BusinessRuleException("Chỉ đổi ca với đồng nghiệp cùng vai trò đang hoạt động.");
        }

        var now = clock.UtcNow;
        if (HasStarted(mine, now) || HasStarted(theirs, now))
        {
            throw new BusinessRuleException("Chỉ đổi ca chưa diễn ra.");
        }

        if (mine.WorkDate == theirs.WorkDate && mine.ShiftId == theirs.ShiftId)
        {
            throw new BusinessRuleException("Hai ca giống nhau, không cần đổi.");
        }

        if (await staffing.AssignmentExistsAsync(user.AccountId, theirs.WorkDate, theirs.ShiftId))
        {
            throw new BusinessRuleException($"Bạn đã có {Describe(theirs)}.");
        }

        if (await staffing.AssignmentExistsAsync(theirs.AccountId, mine.WorkDate, mine.ShiftId))
        {
            throw new BusinessRuleException($"{theirs.Account.FullName} đã có {Describe(mine)}.");
        }

        if (await staffing.HasPendingSwapAsync(mine.Id) || await staffing.HasPendingSwapAsync(theirs.Id))
        {
            throw new ConflictException("Một trong hai ca đang nằm trong đơn đổi ca khác chờ duyệt.");
        }

        var created = await staffing.AddLeaveRequestAsync(new LeaveRequest
        {
            AccountId = user.AccountId,
            Type = LeaveRequestType.Swap,
            FromDate = mine.WorkDate < theirs.WorkDate ? mine.WorkDate : theirs.WorkDate,
            ToDate = mine.WorkDate > theirs.WorkDate ? mine.WorkDate : theirs.WorkDate,
            ShiftAssignmentId = mine.Id,
            SwapWithAccountId = theirs.AccountId,
            SwapShiftAssignmentId = theirs.Id,
            Reason = request.Reason.Trim(),
            CreatedAt = now
        });
        var body = $"{user.Name} đổi {Describe(mine)} lấy {Describe(theirs)} của {theirs.Account.FullName}. Lý do: {created.Reason}";
        await notifications.NotifyRolesAsync([Roles.Admin], NotificationType.System, "Đơn đổi ca mới", body, LeaveLink);
        await notifications.NotifyAsync([theirs.AccountId], NotificationType.System, $"{user.Name} xin đổi ca với bạn",
            $"{body} Đơn chờ Admin duyệt.", LeaveLink);
        return created;
    }

    private async Task NotifyAutoCancelledAsync(IEnumerable<LeaveRequest> cancelled)
    {
        foreach (var item in cancelled)
        {
            int[] receivers = item.SwapWithAccountId is int colleague ? [item.AccountId, colleague] : [item.AccountId];
            await notifications.NotifyAsync(receivers, NotificationType.System, "Đơn đổi ca tự hủy", item.ReviewNote, LeaveLink);
        }
    }

    /// <summary>Người ca kế tiếp của mỗi (ngày, ca) có bàn giao trong khoảng — tính một lần cho cả danh sách.</summary>
    private async Task<AckContext> LoadAckContextAsync(DateOnly from, DateOnly to)
    {
        var shifts = await staffing.ListShiftsAsync(activeOnly: false);
        var assignments = await staffing.ListAssignmentsAsync(from, to.AddDays(1), null);
        return new AckContext(shifts, assignments.Select(item => (item.AccountId, item.WorkDate, item.ShiftId)).ToHashSet());
    }

    private static bool CanAcknowledge(CurrentUser user, ShiftHandover handover, AckContext context)
    {
        if (handover.AcknowledgedAt is not null || handover.FromAccountId == user.AccountId)
        {
            return false;
        }

        if (user.IsInRole(Roles.Admin, Roles.Nurse))
        {
            return true;
        }

        return StaffingRules.NextShift(handover.WorkDate, handover.Shift, context.Shifts) is { } next
            && context.Assignments.Contains((user.AccountId, next.WorkDate, next.Shift.Id));
    }

    /// <summary>Caregiver chỉ thấy bàn giao của mình hoặc bàn giao mình cần nhận; vai trò khác thấy tất cả.</summary>
    private static bool CanSee(CurrentUser user, HandoverDto handover) =>
        !user.IsInRole(Roles.Caregiver) || handover.FromAccountId == user.AccountId || handover.CanAcknowledge;

    private async Task<HandoverDto> GetHandoverDtoAsync(CurrentUser user, long id)
    {
        var handover = await staffing.GetHandoverAsync(id) ?? throw new NotFoundException("Không tìm thấy bàn giao.");
        var context = await LoadAckContextAsync(handover.WorkDate, handover.WorkDate);
        return handover.ToDto(CanAcknowledge(user, handover, context));
    }

    private async Task<LeaveRequestDto> GetLeaveDtoAsync(int id) =>
        (await staffing.GetLeaveRequestAsync(id) ?? throw new NotFoundException("Không tìm thấy đơn.")).ToDto();

    private async Task<Shift> RequireShiftAsync(int shiftId)
    {
        var shift = await staffing.GetShiftAsync(shiftId);
        return shift is { IsActive: true } ? shift : throw new NotFoundException("Không tìm thấy ca làm việc.");
    }

    private static int[] Involved(LeaveRequest request) =>
        request.SwapWithAccountId is int colleague ? [request.AccountId, colleague] : [request.AccountId];

    private static string Describe(ShiftAssignment? assignment) =>
        assignment is null ? "(ca đã bị bỏ)" : $"{assignment.Shift.Name.ToLowerInvariant()} {assignment.WorkDate:dd/MM}";

    private static string Local(DateTime utc) => (utc + FacilityClock.Offset).ToString("HH:mm");

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private sealed record AckContext(List<Shift> Shifts, HashSet<(int AccountId, DateOnly WorkDate, int ShiftId)> Assignments);
}
