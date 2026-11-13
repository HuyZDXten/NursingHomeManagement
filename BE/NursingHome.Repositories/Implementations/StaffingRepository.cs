using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class StaffingRepository : IStaffingRepository
{
    private static StaffingDAO Dao => StaffingDAO.Instance;

    public Task<List<Shift>> ListShiftsAsync(bool activeOnly) => Dao.ListShiftsAsync(activeOnly);

    public Task<Shift?> GetShiftAsync(int id) => Dao.GetShiftAsync(id);

    public Task<List<ShiftAssignment>> ListAssignmentsAsync(DateOnly from, DateOnly to, int? accountId) => Dao.ListAssignmentsAsync(from, to, accountId);

    public Task<ShiftAssignment?> GetAssignmentAsync(int id) => Dao.GetAssignmentAsync(id);

    public Task<bool> AssignmentExistsAsync(int accountId, DateOnly workDate, int shiftId) => Dao.AssignmentExistsAsync(accountId, workDate, shiftId);

    public Task<ShiftAssignment> AddAssignmentAsync(ShiftAssignment assignment) => Dao.AddAssignmentAsync(assignment);

    public Task DeleteAssignmentAsync(int id) => Dao.DeleteAssignmentAsync(id);

    public Task<(List<ShiftAssignment> Added, int Skipped)> CopyWeekAsync(DateOnly fromMonday, DateOnly toMonday, DateOnly earliest, int byAccountId, DateTime now) =>
        Dao.CopyWeekAsync(fromMonday, toMonday, earliest, byAccountId, now);

    public Task<List<ResidentCareAssignment>> ListCareAssignmentsAsync(int? residentId, int? accountId, bool activeOnly) =>
        Dao.ListCareAssignmentsAsync(residentId, accountId, activeOnly);

    public Task<ResidentCareAssignment?> GetCareAssignmentAsync(int id) => Dao.GetCareAssignmentAsync(id);

    public Task<ResidentCareAssignment> AddCareAssignmentAsync(ResidentCareAssignment assignment) => Dao.AddCareAssignmentAsync(assignment);

    public Task EndCareAssignmentAsync(int id, DateOnly endDate) => Dao.EndCareAssignmentAsync(id, endDate);

    public Task<bool> IsPrimaryCaregiverAsync(int residentId, int accountId, DateOnly today) => Dao.IsPrimaryCaregiverAsync(residentId, accountId, today);

    public Task<List<int>> PrimaryResidentIdsAsync(int accountId, DateOnly today) => Dao.PrimaryResidentIdsAsync(accountId, today);
}
