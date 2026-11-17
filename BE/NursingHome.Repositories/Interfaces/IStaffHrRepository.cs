using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;

namespace NursingHome.Repositories.Interfaces;

/// <summary>BL-V4: hồ sơ & chứng chỉ nhân viên, chấm công, dữ liệu hiệu suất.</summary>
public interface IStaffHrRepository
{
    Task<List<Account>> ListStaffAccountsAsync(IReadOnlyCollection<string> roles);

    Task<List<StaffProfile>> ListProfilesAsync();

    Task<StaffProfile?> GetProfileAsync(int accountId);

    Task UpsertProfileAsync(StaffProfile profile);

    Task<List<StaffCertificate>> ListCertificatesAsync(int? accountId);

    Task<StaffCertificate?> GetCertificateAsync(int id);

    Task<StaffCertificate> AddCertificateAsync(StaffCertificate certificate);

    Task<bool> UpdateCertificateAsync(int id, Action<StaffCertificate> apply);

    Task<bool> DeleteCertificateAsync(int id);

    Task<List<StaffCertificate>> ListExpiringAsync(DateOnly until);

    Task<bool> MarkAlertedAsync(int id, bool expiredStage, DateTime now);

    Task<ShiftAssignment?> GetAssignmentAsync(int id);

    Task<Attendance?> GetAttendanceAsync(int assignmentId);

    Task<List<AssignmentFact>> ListAssignmentFactsAsync(int? accountId, DateOnly from, DateOnly to);

    Task<Attendance> CheckInAsync(Attendance attendance);

    Task<bool> CheckOutAsync(int assignmentId, int accountId, DateTime now);

    Task<Attendance> UpsertEditAsync(int assignmentId, int accountId, DateTime checkInAt, DateTime? checkOutAt, int lateMinutes, string reason, int editorId, DateTime now);

    Task<List<TaskFact>> TaskFactsAsync(DateTime fromUtc, DateTime toUtc);

    Task<Dictionary<int, int>> MedicationRecordCountsAsync(DateTime fromUtc, DateTime toUtc);

    Task<List<CareTask>> TasksForTodayAsync(int accountId, DateTime startOfDayUtc, DateTime endOfDayUtc);
}
