using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Common;
using NursingHome.DTOs.Staffing;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>
/// S3-V2 — UC-44 lịch ca tuần (Admin xếp), UC-45 phân công phụ trách cư dân (Admin/Nurse), UC-46 lịch ca của tôi.
/// Chỉ xếp ca cho Doctor/Nurse/Caregiver đang hoạt động, không sửa lịch đã qua.
/// </summary>
public class StaffingService(
    IStaffingRepository staffing,
    IAccountRepository accounts,
    IResidentRepository residents,
    INotificationService notifications,
    FacilityClock clock) : IStaffingService
{
    private const string ShiftsLink = "/staff/shifts";
    private const int MaxRangeDays = 62;

    private static readonly string[] ShiftRoles = [Roles.Doctor, Roles.Nurse, Roles.Caregiver];
    private static readonly string[] PrimaryCaregiverRoles = [Roles.Nurse, Roles.Caregiver];

    // ---------------- Lịch ca ----------------

    public async Task<List<ShiftDto>> ListShiftsAsync() =>
        (await staffing.ListShiftsAsync(activeOnly: true)).Select(shift => shift.ToDto()).ToList();

    public async Task<List<ShiftAssignmentDto>> ListAssignmentsAsync(CurrentUser user, DateOnly? from, DateOnly? to)
    {
        var start = from ?? StaffingRules.WeekStart(clock.Today);
        var end = to ?? start.AddDays(6);
        if (end < start || end.DayNumber - start.DayNumber >= MaxRangeDays)
        {
            throw new BusinessRuleException($"Khoảng ngày không hợp lệ (tối đa {MaxRangeDays} ngày).");
        }

        int? onlyAccount = user.IsInRole(Roles.Admin, Roles.Nurse) ? null : user.AccountId;
        return (await staffing.ListAssignmentsAsync(start, end, onlyAccount)).Select(item => item.ToDto()).ToList();
    }

    public async Task<List<ShiftAssignmentDto>> MyAssignmentsAsync(CurrentUser user, int days)
    {
        var today = clock.Today;
        var count = Math.Clamp(days, 1, MaxRangeDays);
        return (await staffing.ListAssignmentsAsync(today, today.AddDays(count - 1), user.AccountId))
            .Select(item => item.ToDto()).ToList();
    }

    public async Task<ShiftAssignmentDto> CreateAssignmentAsync(CurrentUser user, CreateShiftAssignmentRequest request)
    {
        if (request.WorkDate < clock.Today)
        {
            throw new BusinessRuleException("Không xếp ca cho ngày đã qua.");
        }

        if (!await accounts.IsActiveInRolesAsync(request.AccountId, ShiftRoles))
        {
            throw new BusinessRuleException("Chỉ xếp ca cho bác sĩ, điều dưỡng hoặc hộ lý đang hoạt động.");
        }

        var shift = await staffing.GetShiftAsync(request.ShiftId);
        if (shift is null || !shift.IsActive)
        {
            throw new NotFoundException("Không tìm thấy ca làm việc.");
        }

        if (await staffing.AssignmentExistsAsync(request.AccountId, request.WorkDate, request.ShiftId))
        {
            throw new ConflictException($"Nhân viên đã được xếp {shift.Name.ToLowerInvariant()} ngày {request.WorkDate:dd/MM/yyyy}.");
        }

        var neighbours = await staffing.ListAssignmentsAsync(request.WorkDate.AddDays(-1), request.WorkDate.AddDays(1), request.AccountId);
        var created = await staffing.AddAssignmentAsync(new ShiftAssignment
        {
            AccountId = request.AccountId,
            ShiftId = request.ShiftId,
            WorkDate = request.WorkDate,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedById = user.AccountId,
            CreatedAt = clock.UtcNow
        });

        await notifications.NotifyAsync([request.AccountId], NotificationType.System, "Lịch ca mới",
            $"{shift.Name} ngày {request.WorkDate:dd/MM/yyyy} ({shift.StartTime:HH\\:mm}–{shift.EndTime:HH\\:mm}).", ShiftsLink);

        var detail = await staffing.GetAssignmentAsync(created.Id) ?? throw new NotFoundException("Không tìm thấy ca đã xếp.");
        return detail.ToDto(AdjacentWarning(detail, neighbours));
    }

    public async Task DeleteAssignmentAsync(int id)
    {
        var assignment = await staffing.GetAssignmentAsync(id) ?? throw new NotFoundException("Không tìm thấy ca đã xếp.");
        if (assignment.WorkDate < clock.Today)
        {
            throw new BusinessRuleException("Không sửa lịch ca đã qua.");
        }

        await staffing.DeleteAssignmentAsync(id);
        await notifications.NotifyAsync([assignment.AccountId], NotificationType.System, "Lịch ca thay đổi",
            $"Đã bỏ {assignment.Shift.Name.ToLowerInvariant()} ngày {assignment.WorkDate:dd/MM/yyyy}.", ShiftsLink);
    }

    public async Task<CopyWeekResultDto> CopyWeekAsync(CurrentUser user, CopyWeekRequest request)
    {
        if (request.FromWeekStart.DayOfWeek != DayOfWeek.Monday || request.ToWeekStart.DayOfWeek != DayOfWeek.Monday)
        {
            throw new BusinessRuleException("Ngày bắt đầu tuần phải là thứ Hai.");
        }

        if (request.FromWeekStart == request.ToWeekStart)
        {
            throw new BusinessRuleException("Tuần nguồn và tuần đích phải khác nhau.");
        }

        if (request.ToWeekStart.AddDays(6) < clock.Today)
        {
            throw new BusinessRuleException("Không chép lịch vào tuần đã qua.");
        }

        var (added, skipped) = await staffing.CopyWeekAsync(request.FromWeekStart, request.ToWeekStart, clock.Today, user.AccountId, clock.UtcNow);
        var affected = added.Select(item => item.AccountId).Distinct().ToList();
        if (affected.Count > 0)
        {
            await notifications.NotifyAsync(affected, NotificationType.System, "Lịch ca tuần mới",
                $"Đã xếp lịch ca tuần {request.ToWeekStart:dd/MM} – {request.ToWeekStart.AddDays(6):dd/MM/yyyy}.", ShiftsLink);
        }

        return new CopyWeekResultDto { Copied = added.Count, Skipped = skipped };
    }

    // ---------------- Phân công phụ trách ----------------

    /// <summary>Caregiver chỉ thấy phân công của chính mình (không lộ danh sách cư dân ngoài phạm vi được xem).</summary>
    public async Task<List<CareAssignmentDto>> ListCareAssignmentsAsync(CurrentUser user, int? residentId, int? accountId, bool activeOnly)
    {
        var onlyAccount = user.IsInRole(Roles.Caregiver) ? user.AccountId : accountId;
        return (await staffing.ListCareAssignmentsAsync(residentId, onlyAccount, activeOnly)).Select(item => item.ToDto()).ToList();
    }

    public async Task<CareAssignmentDto> CreateCareAssignmentAsync(CurrentUser user, CreateCareAssignmentRequest request)
    {
        var today = clock.Today;
        var startDate = request.StartDate ?? today;
        if (startDate < today)
        {
            throw new BusinessRuleException("Ngày bắt đầu phân công không được ở quá khứ.");
        }

        var resident = await residents.GetByIdAsync(request.ResidentId) ?? throw new NotFoundException("Không tìm thấy cư dân.");
        if (resident.Status != ResidentStatus.Active)
        {
            throw new BusinessRuleException("Chỉ phân công phụ trách cư dân đang lưu trú.");
        }

        if (!await accounts.IsActiveInRolesAsync(request.AccountId, PrimaryCaregiverRoles))
        {
            throw new BusinessRuleException("Người phụ trách phải là điều dưỡng hoặc hộ lý đang hoạt động.");
        }

        Shift? shift = null;
        if (request.ShiftId is int shiftId)
        {
            shift = await staffing.GetShiftAsync(shiftId);
            if (shift is null || !shift.IsActive)
            {
                throw new NotFoundException("Không tìm thấy ca làm việc.");
            }
        }

        var created = await staffing.AddCareAssignmentAsync(new ResidentCareAssignment
        {
            ResidentId = request.ResidentId,
            AccountId = request.AccountId,
            ShiftId = request.ShiftId,
            StartDate = startDate,
            AssignedById = user.AccountId,
            CreatedAt = clock.UtcNow
        });

        if (request.AccountId != user.AccountId)
        {
            await notifications.NotifyAsync([request.AccountId], NotificationType.Care, "Bạn được phân công phụ trách cư dân",
                $"{resident.Code} · {resident.FullName} — {(shift?.Name ?? "mọi ca")}, từ {startDate:dd/MM/yyyy}.",
                $"/staff/residents/{resident.Id}");
        }

        return await GetCareAssignmentDtoAsync(created.Id);
    }

    public async Task<CareAssignmentDto> EndCareAssignmentAsync(int id)
    {
        await staffing.EndCareAssignmentAsync(id, clock.Today);
        return await GetCareAssignmentDtoAsync(id);
    }

    // ---------------- Nội bộ ----------------

    private async Task<CareAssignmentDto> GetCareAssignmentDtoAsync(int id) =>
        (await staffing.GetCareAssignmentAsync(id) ?? throw new NotFoundException("Không tìm thấy phân công phụ trách.")).ToDto();

    /// <summary>Ca mới nối liền/chồng ca đã có của cùng người (hôm trước, cùng ngày, hôm sau) → cảnh báo, vẫn cho xếp.</summary>
    private static string? AdjacentWarning(ShiftAssignment created, IEnumerable<ShiftAssignment> neighbours)
    {
        var window = StaffingRules.ShiftWindow(created.WorkDate, created.Shift);
        var touching = neighbours
            .Where(item => item.Id != created.Id && StaffingRules.Touches(window, StaffingRules.ShiftWindow(item.WorkDate, item.Shift)))
            .Select(item => $"{item.Shift.Name.ToLowerInvariant()} {item.WorkDate:dd/MM}")
            .ToList();
        return touching.Count == 0
            ? null
            : $"{created.Account.FullName} làm liền {string.Join(", ", touching)} — kiểm tra thời gian nghỉ.";
    }
}
