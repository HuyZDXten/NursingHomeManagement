using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class StaffHrRepository : IStaffHrRepository
{
    private static StaffHrDAO Dao => StaffHrDAO.Instance;

    public Task<List<Account>> ListStaffAccountsAsync(IReadOnlyCollection<string> roles) => Dao.ListStaffAccountsAsync(roles);

    public Task<List<StaffProfile>> ListProfilesAsync() => Dao.ListProfilesAsync();

    public Task<StaffProfile?> GetProfileAsync(int accountId) => Dao.GetProfileAsync(accountId);

    public Task UpsertProfileAsync(StaffProfile profile) => Dao.UpsertProfileAsync(profile);

    public Task<List<StaffCertificate>> ListCertificatesAsync(int? accountId) => Dao.ListCertificatesAsync(accountId);

    public Task<StaffCertificate?> GetCertificateAsync(int id) => Dao.GetCertificateAsync(id);

    public Task<StaffCertificate> AddCertificateAsync(StaffCertificate certificate) => Dao.AddCertificateAsync(certificate);

    public Task<bool> UpdateCertificateAsync(int id, Action<StaffCertificate> apply) => Dao.UpdateCertificateAsync(id, apply);

    public Task<bool> DeleteCertificateAsync(int id) => Dao.DeleteCertificateAsync(id);

    public Task<List<StaffCertificate>> ListExpiringAsync(DateOnly until) => Dao.ListExpiringAsync(until);

    public Task<bool> MarkAlertedAsync(int id, bool expiredStage, DateTime now) => Dao.MarkAlertedAsync(id, expiredStage, now);

    public Task<ShiftAssignment?> GetAssignmentAsync(int id) => Dao.GetAssignmentAsync(id);

    public Task<Attendance?> GetAttendanceAsync(int assignmentId) => Dao.GetAttendanceAsync(assignmentId);

    public Task<List<AssignmentFact>> ListAssignmentFactsAsync(int? accountId, DateOnly from, DateOnly to) =>
        Dao.ListAssignmentFactsAsync(accountId, from, to);

    public Task<Attendance> CheckInAsync(Attendance attendance) => Dao.CheckInAsync(attendance);

    public Task<bool> CheckOutAsync(int assignmentId, int accountId, DateTime now) => Dao.CheckOutAsync(assignmentId, accountId, now);

    public Task<Attendance> UpsertEditAsync(int assignmentId, int accountId, DateTime checkInAt, DateTime? checkOutAt, int lateMinutes, string reason, int editorId, DateTime now) =>
        Dao.UpsertEditAsync(assignmentId, accountId, checkInAt, checkOutAt, lateMinutes, reason, editorId, now);

    public Task<List<TaskFact>> TaskFactsAsync(DateTime fromUtc, DateTime toUtc) => Dao.TaskFactsAsync(fromUtc, toUtc);

    public Task<Dictionary<int, int>> MedicationRecordCountsAsync(DateTime fromUtc, DateTime toUtc) => Dao.MedicationRecordCountsAsync(fromUtc, toUtc);

    public Task<List<CareTask>> TasksForTodayAsync(int accountId, DateTime startOfDayUtc, DateTime endOfDayUtc) =>
        Dao.TasksForTodayAsync(accountId, startOfDayUtc, endOfDayUtc);
}
