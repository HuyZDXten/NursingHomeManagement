using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DataAccessObjects;
using NursingHome.Services.Helpers;

namespace NursingHome.Tests;

/// <summary>BL-V4: chứng chỉ, chấm công, hiệu suất nhân viên.</summary>
public class StaffHrTests
{
    private static readonly DateOnly Today = new(2026, 11, 17);

    // Ca sáng 06:00–14:00 giờ cơ sở (UTC+7) = 23:00 hôm trước – 07:00 UTC.
    private static readonly DateTime Start = new(2026, 11, 16, 23, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 11, 17, 7, 0, 0, DateTimeKind.Utc);

    // ---------------- Chứng chỉ ----------------

    [Fact]
    public void Certificate_StateByDaysLeft()
    {
        Assert.Equal(CertificateState.Valid, StaffHrRules.StateOf(null, Today));
        Assert.Equal(CertificateState.Valid, StaffHrRules.StateOf(Today.AddDays(31), Today));
        Assert.Equal(CertificateState.ExpiringSoon, StaffHrRules.StateOf(Today.AddDays(30), Today));
        Assert.Equal(CertificateState.ExpiringSoon, StaffHrRules.StateOf(Today, Today)); // hết hạn hôm nay vẫn còn dùng
        Assert.Equal(CertificateState.Expired, StaffHrRules.StateOf(Today.AddDays(-1), Today));
        Assert.Equal(20, StaffHrRules.DaysLeft(Today.AddDays(20), Today));
        Assert.Equal(-5, StaffHrRules.DaysLeft(Today.AddDays(-5), Today));
        Assert.Null(StaffHrRules.DaysLeft(null, Today));
    }

    [Fact]
    public void Certificate_EachMilestoneIsAlertedOnce()
    {
        var soon = new StaffCertificate { ExpiresOn = Today.AddDays(20) };
        Assert.Equal(CertificateState.ExpiringSoon, StaffHrRules.PendingAlert(soon, Today));
        soon.Alert30At = DateTime.UtcNow;
        Assert.Null(StaffHrRules.PendingAlert(soon, Today)); // đã báo mốc 30 ngày

        var expired = new StaffCertificate { ExpiresOn = Today.AddDays(-5) };
        Assert.Equal(CertificateState.Expired, StaffHrRules.PendingAlert(expired, Today));
        expired.AlertExpiredAt = DateTime.UtcNow;
        Assert.Null(StaffHrRules.PendingAlert(expired, Today));

        // Đã báo "còn 30 ngày", sau đó quá hạn → được báo thêm mốc "hết hạn".
        var later = new StaffCertificate { ExpiresOn = Today.AddDays(-1), Alert30At = DateTime.UtcNow };
        Assert.Equal(CertificateState.Expired, StaffHrRules.PendingAlert(later, Today));

        Assert.Null(StaffHrRules.PendingAlert(new StaffCertificate { ExpiresOn = null }, Today));
        Assert.Null(StaffHrRules.PendingAlert(new StaffCertificate { ExpiresOn = Today.AddDays(90) }, Today));
    }

    [Fact]
    public void Certificate_Validation()
    {
        StaffHrRules.ValidateCertificate("CCHN", Today.AddYears(-1), Today.AddYears(2), Today);
        StaffHrRules.ValidateCertificate("Sơ cấp cứu", null, null, Today);
        Assert.Throws<BusinessRuleException>(() => StaffHrRules.ValidateCertificate(" ", null, null, Today));
        Assert.Throws<BusinessRuleException>(() => StaffHrRules.ValidateCertificate("X", Today, Today.AddDays(-1), Today));
        Assert.Throws<BusinessRuleException>(() => StaffHrRules.ValidateCertificate("X", Today.AddDays(1), null, Today));
    }

    // ---------------- Chấm công ----------------

    [Fact]
    public void CheckIn_WindowIsThirtyMinutesBeforeStartUntilEnd()
    {
        Assert.False(StaffHrRules.CanCheckIn(Start, End, Start.AddMinutes(-31)));
        Assert.True(StaffHrRules.CanCheckIn(Start, End, Start.AddMinutes(-30)));
        Assert.True(StaffHrRules.CanCheckIn(Start, End, Start.AddHours(4)));
        Assert.True(StaffHrRules.CanCheckIn(Start, End, End));
        Assert.False(StaffHrRules.CanCheckIn(Start, End, End.AddMinutes(1)));
    }

    [Theory]
    [InlineData(-20, 0)]
    [InlineData(0, 0)]
    [InlineData(15, 0)]
    [InlineData(16, 16)]
    [InlineData(25, 25)]
    [InlineData(95, 95)]
    public void LateMinutes_GraceIsFifteenMinutes(int minutesAfterStart, int expected)
    {
        Assert.Equal(expected, StaffHrRules.LateMinutes(Start, Start.AddMinutes(minutesAfterStart)));
    }

    [Fact]
    public void Edit_RequiresReasonAndSaneTimes()
    {
        var now = End.AddDays(2);
        StaffHrRules.ValidateEdit(Start, End, Start.AddMinutes(5), End, "Quên chấm công", now);
        Assert.Throws<BusinessRuleException>(() => StaffHrRules.ValidateEdit(Start, End, Start, End, " ", now));
        Assert.Throws<BusinessRuleException>(() => StaffHrRules.ValidateEdit(Start, End, null, End, "x", now));
        Assert.Throws<BusinessRuleException>(() => StaffHrRules.ValidateEdit(Start, End, Start.AddHours(2), Start.AddHours(1), "x", now));
        Assert.Throws<BusinessRuleException>(() => StaffHrRules.ValidateEdit(Start, End, now.AddHours(1), null, "x", now)); // tương lai
        Assert.Throws<BusinessRuleException>(() => StaffHrRules.ValidateEdit(Start, End, Start.AddDays(-3), null, "x", now)); // lệch quá xa ca
        Assert.Throws<BusinessRuleException>(() => StaffHrRules.ValidateEdit(Start, End, Start, End, "x", Start.AddDays(40))); // quá 31 ngày
    }

    // ---------------- Hiệu suất ----------------

    private static Account Staff(int id, string name, string role) => new() { Id = id, FullName = name, Role = role };

    private static Shift Morning() => new() { Id = 1, Code = "MORNING", Name = "Ca sáng", StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(14, 0) };

    [Fact]
    public void Performance_OnTimeRateUsesFifteenMinuteGraceAndOnlyTasksAlreadyDue()
    {
        var now = new DateTime(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);
        var due = now.AddHours(-5);
        var tasks = new List<TaskFact>
        {
            new(5, due, CareTaskStatus.Completed, due.AddMinutes(-10)),            // sớm: đúng hạn
            new(5, due, CareTaskStatus.Completed, due.AddMinutes(15)),             // trong dung sai: đúng hạn
            new(5, due, CareTaskStatus.Completed, due.AddMinutes(40)),             // trễ
            new(5, due, CareTaskStatus.Pending, null),                             // quá hạn chưa xong
            new(5, now.AddHours(3), CareTaskStatus.Pending, null),                 // chưa tới hạn: không tính
            new(6, due, CareTaskStatus.Completed, null)                            // Completed không có giờ: coi là đúng hạn
        };

        var rows = StaffPerformance.Build([Staff(5, "Y tá", Roles.Nurse), Staff(6, "Hộ lý", Roles.Caregiver)], tasks,
            new Dictionary<int, int>(), [], now);

        var nurse = rows.Single(row => row.AccountId == 5);
        Assert.Equal(4, nurse.TasksDue);
        Assert.Equal(2, nurse.TasksOnTime);
        Assert.Equal(1, nurse.TasksLate);
        Assert.Equal(1, nurse.TasksOverdueOpen);
        Assert.Equal(50m, nurse.OnTimeRate);
        Assert.Equal(100m, rows.Single(row => row.AccountId == 6).OnTimeRate);
    }

    [Fact]
    public void Performance_AttendanceCountsOnlyEndedShifts()
    {
        var now = new DateTime(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc); // 17:00 giờ cơ sở ngày 17/11
        var shift = Morning();
        Attendance Present(int late) => new() { CheckInAt = now, LateMinutes = late };
        var assignments = new List<AssignmentFact>
        {
            new(1, 6, new DateOnly(2026, 11, 12), shift, Present(0)),
            new(2, 6, new DateOnly(2026, 11, 13), shift, Present(25)),
            new(3, 6, new DateOnly(2026, 11, 14), shift, null),                  // vắng
            new(4, 6, new DateOnly(2026, 11, 17), shift, null),                  // ca sáng 17/11 đã kết thúc 14:00 → vắng
            new(5, 6, new DateOnly(2026, 11, 18), shift, null)                   // ngày mai: chưa tính
        };

        var row = Assert.Single(StaffPerformance.Build([Staff(6, "Hộ lý", Roles.Caregiver)], [], new Dictionary<int, int>(), assignments, now));
        Assert.Equal(4, row.ShiftsAssigned);
        Assert.Equal(2, row.ShiftsAttended);
        Assert.Equal(1, row.LateCount);
        Assert.Equal(2, row.Absent);
        Assert.Equal(50m, row.AttendanceRate);
    }

    [Fact]
    public void Performance_SkipsStaffWithNoActivityAndOrdersByRoleThenName()
    {
        var now = new DateTime(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);
        var rows = StaffPerformance.Build(
            [Staff(1, "Bình", Roles.Nurse), Staff(2, "An", Roles.Nurse), Staff(3, "Không việc", Roles.Doctor), Staff(4, "Cường", Roles.Caregiver)],
            [], new Dictionary<int, int> { [1] = 3, [2] = 1, [4] = 2 }, [], now);

        Assert.Equal(["Cường", "An", "Bình"], rows.Select(row => row.FullName)); // Caregiver < Nurse theo bảng chữ cái
        Assert.DoesNotContain(rows, row => row.AccountId == 3);
        Assert.Equal(3, rows.Single(row => row.AccountId == 1).MedicationRecords);
    }

    [Fact]
    public void Percent_RoundsToOneDecimalAndHandlesZero()
    {
        Assert.Null(StaffHrRules.Percent(0, 0));
        Assert.Equal(66.7m, StaffHrRules.Percent(2, 3));
        Assert.Equal(100m, StaffHrRules.Percent(3, 2));
    }

    // ---------------- Trạng thái ca của tôi ----------------

    [Fact]
    public void MyShift_StatusFollowsClockAndAttendance()
    {
        var shift = Morning();
        var workDate = new DateOnly(2026, 11, 17);
        var (start, end) = StaffingRules.ShiftWindow(workDate, shift);

        Assert.Equal("Upcoming", new AssignmentFact(1, 5, workDate, shift, null).ToMyShift(start.AddMinutes(-31)).Status);
        var open = new AssignmentFact(1, 5, workDate, shift, null).ToMyShift(start.AddMinutes(-10));
        Assert.Equal("Open", open.Status);
        Assert.True(open.CanCheckIn);
        Assert.False(open.CanCheckOut);
        Assert.Equal("Missed", new AssignmentFact(1, 5, workDate, shift, null).ToMyShift(end.AddMinutes(1)).Status);

        var working = new AssignmentFact(1, 5, workDate, shift, new Attendance { CheckInAt = start, LateMinutes = 0 }).ToMyShift(start.AddHours(2));
        Assert.Equal("InProgress", working.Status);
        Assert.True(working.CanCheckOut);
        Assert.False(working.CanCheckIn);

        var done = new AssignmentFact(1, 5, workDate, shift, new Attendance { CheckInAt = start, CheckOutAt = end }).ToMyShift(end.AddHours(1));
        Assert.Equal("Done", done.Status);
        Assert.False(done.CanCheckOut);
    }
}
