using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Common;
using NursingHome.DTOs.Reports;
using NursingHome.DTOs.Staffing;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>
/// BL-V4: chấm công (UC-48) và hiệu suất nhân viên (UC-126).
/// Vào ca từ 30 phút trước giờ bắt đầu tới hết ca; trễ quá 15 phút tính đi muộn. Chỉ Admin sửa công và phải ghi lý do.
/// </summary>
public class AttendanceService(IStaffHrRepository hr, FacilityClock clock) : IAttendanceService
{
    private const int MaxListDays = 62;
    private const int MaxPerformanceDays = 366;

    private static readonly string[] PerformanceRoles = [Roles.Nurse, Roles.Caregiver, Roles.Doctor];

    public async Task<List<MyShiftDto>> MyShiftsAsync(CurrentUser user)
    {
        var today = clock.Today;
        var now = clock.UtcNow;
        var facts = await hr.ListAssignmentFactsAsync(user.AccountId, today.AddDays(-1), today.AddDays(7));
        return facts.Select(fact => fact.ToMyShift(now)).OrderBy(shift => shift.StartsAt).ToList();
    }

    public async Task<MyShiftDto> CheckInAsync(CurrentUser user, int shiftAssignmentId)
    {
        var assignment = await RequireMineAsync(user, shiftAssignmentId);
        var (start, end) = StaffingRules.ShiftWindow(assignment.WorkDate, assignment.Shift);
        var now = clock.UtcNow;
        if (now < start.AddMinutes(-StaffHrRules.CheckInEarlyMinutes))
        {
            throw new BusinessRuleException($"Chưa tới giờ vào ca. Vào ca được từ {StaffHrRules.CheckInEarlyMinutes} phút trước giờ bắt đầu.");
        }

        if (now > end)
        {
            throw new BusinessRuleException("Ca đã kết thúc. Nhờ quản trị viên bổ sung công nếu bạn có đi làm.");
        }

        await hr.CheckInAsync(new Attendance
        {
            ShiftAssignmentId = assignment.Id,
            AccountId = user.AccountId,
            CheckInAt = now,
            LateMinutes = StaffHrRules.LateMinutes(start, now)
        });
        return await MyShiftAsync(user, shiftAssignmentId);
    }

    public async Task<MyShiftDto> CheckOutAsync(CurrentUser user, int shiftAssignmentId)
    {
        await RequireMineAsync(user, shiftAssignmentId);
        if (!await hr.CheckOutAsync(shiftAssignmentId, user.AccountId, clock.UtcNow))
        {
            throw new BusinessRuleException("Bạn chưa vào ca hoặc đã ra ca rồi.");
        }

        return await MyShiftAsync(user, shiftAssignmentId);
    }

    public async Task<List<AttendanceRecordDto>> ListAsync(DateOnly? from, DateOnly? to, int? accountId)
    {
        var end = to ?? clock.Today;
        var start = from ?? end.AddDays(-13);
        if (end < start || end.DayNumber - start.DayNumber >= MaxListDays)
        {
            throw new BusinessRuleException($"Khoảng ngày không hợp lệ (tối đa {MaxListDays} ngày).");
        }

        var names = (await hr.ListStaffAccountsAsync(Roles.Staff)).ToDictionary(item => item.Id, item => item.FullName);
        var now = clock.UtcNow;
        return (await hr.ListAssignmentFactsAsync(accountId, start, end))
            .Select(fact => fact.ToRecord(now, names.GetValueOrDefault(fact.AccountId, "")))
            .OrderByDescending(record => record.StartsAt).ThenBy(record => record.AccountName).ToList();
    }

    public async Task<AttendanceRecordDto> EditAsync(CurrentUser admin, int shiftAssignmentId, EditAttendanceRequest request)
    {
        var assignment = await hr.GetAssignmentAsync(shiftAssignmentId) ?? throw new NotFoundException("Không tìm thấy phân ca.");
        var (start, end) = StaffingRules.ShiftWindow(assignment.WorkDate, assignment.Shift);
        var now = clock.UtcNow;
        var checkIn = request.CheckInAt?.ToUniversalTime();
        var checkOut = request.CheckOutAt?.ToUniversalTime();
        StaffHrRules.ValidateEdit(start, end, checkIn, checkOut, request.Reason, now);
        await hr.UpsertEditAsync(shiftAssignmentId, assignment.AccountId, checkIn!.Value, checkOut,
            StaffHrRules.LateMinutes(start, checkIn.Value), request.Reason.Trim(), admin.AccountId, now);

        var fact = (await hr.ListAssignmentFactsAsync(assignment.AccountId, assignment.WorkDate, assignment.WorkDate))
            .First(item => item.AssignmentId == shiftAssignmentId);
        return fact.ToRecord(now, assignment.Account.FullName);
    }

    public async Task<StaffPerformanceReportDto> GetPerformanceAsync(DateOnly? from, DateOnly? to)
    {
        var end = to ?? clock.Today;
        var start = from ?? end.AddDays(-29);
        if (end < start || end.DayNumber - start.DayNumber >= MaxPerformanceDays)
        {
            throw new BusinessRuleException($"Khoảng ngày không hợp lệ (tối đa {MaxPerformanceDays} ngày).");
        }

        var fromUtc = clock.StartOfDayUtc(start);
        var toUtc = clock.StartOfDayUtc(end.AddDays(1));
        var staff = (await hr.ListStaffAccountsAsync(PerformanceRoles)).Where(item => item.IsActive).ToList();
        var rows = StaffPerformance.Build(
            staff,
            await hr.TaskFactsAsync(fromUtc, toUtc),
            await hr.MedicationRecordCountsAsync(fromUtc, toUtc),
            await hr.ListAssignmentFactsAsync(null, start, end),
            clock.UtcNow);
        return new StaffPerformanceReportDto { From = start, To = end, Rows = rows };
    }

    public async Task<ExportFile> ExportPerformanceAsync(DateOnly? from, DateOnly? to)
    {
        var report = await GetPerformanceAsync(from, to);
        using var excel = new ExcelExporter().AddSheet("Hiệu suất nhân viên",
            $"Hiệu suất nhân viên {report.From:dd/MM/yyyy} – {report.To:dd/MM/yyyy}", report.Rows,
            [
                new("Nhân viên", r => r.FullName),
                new("Vai trò", r => r.Role),
                new("Việc có hạn", r => r.TasksDue),
                new("Đúng hạn", r => r.TasksOnTime),
                new("Trễ", r => r.TasksLate),
                new("Quá hạn chưa xong", r => r.TasksOverdueOpen),
                new("Đúng hạn (%)", r => r.OnTimeRate, ExcelExporter.PercentFormat),
                new("Lần ghi thuốc", r => r.MedicationRecords),
                new("Ca đã kết thúc", r => r.ShiftsAssigned),
                new("Có chấm công", r => r.ShiftsAttended),
                new("Đi muộn", r => r.LateCount),
                new("Vắng", r => r.Absent),
                new("Chuyên cần (%)", r => r.AttendanceRate, ExcelExporter.PercentFormat)
            ], "Đúng hạn = hoàn thành trong hạn + 15 phút. Đi muộn = vào ca trễ quá 15 phút. Số liệu việc chăm sóc và thuốc đọc từ bảng của module Chăm sóc.");
        return new ExportFile(excel.ToBytes(), $"hieu-suat-nhan-vien-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.xlsx");
    }

    // ---------------- Hỗ trợ ----------------

    private async Task<ShiftAssignment> RequireMineAsync(CurrentUser user, int shiftAssignmentId)
    {
        var assignment = await hr.GetAssignmentAsync(shiftAssignmentId) ?? throw new NotFoundException("Không tìm thấy ca.");
        return assignment.AccountId == user.AccountId ? assignment : throw new ForbiddenException("Đây không phải ca của bạn.");
    }

    private async Task<MyShiftDto> MyShiftAsync(CurrentUser user, int shiftAssignmentId)
    {
        var assignment = await hr.GetAssignmentAsync(shiftAssignmentId) ?? throw new NotFoundException("Không tìm thấy ca.");
        var fact = (await hr.ListAssignmentFactsAsync(user.AccountId, assignment.WorkDate, assignment.WorkDate))
            .First(item => item.AssignmentId == shiftAssignmentId);
        return fact.ToMyShift(clock.UtcNow);
    }
}
