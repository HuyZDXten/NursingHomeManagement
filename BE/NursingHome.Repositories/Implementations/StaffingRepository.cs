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

    public Task<List<LeaveRequest>> DeleteAssignmentAsync(int id, DateTime now) => Dao.DeleteAssignmentAsync(id, now);

    public Task<(List<ShiftAssignment> Added, int Skipped)> CopyWeekAsync(DateOnly fromMonday, DateOnly toMonday, DateOnly earliest, int byAccountId, DateTime now) =>
        Dao.CopyWeekAsync(fromMonday, toMonday, earliest, byAccountId, now);

    public Task<List<ResidentCareAssignment>> ListCareAssignmentsAsync(int? residentId, int? accountId, bool activeOnly) =>
        Dao.ListCareAssignmentsAsync(residentId, accountId, activeOnly);

    public Task<ResidentCareAssignment?> GetCareAssignmentAsync(int id) => Dao.GetCareAssignmentAsync(id);

    public Task<ResidentCareAssignment> AddCareAssignmentAsync(ResidentCareAssignment assignment) => Dao.AddCareAssignmentAsync(assignment);

    public Task EndCareAssignmentAsync(int id, DateOnly endDate) => Dao.EndCareAssignmentAsync(id, endDate);

    public Task<bool> IsPrimaryCaregiverAsync(int residentId, int accountId, DateOnly today) => Dao.IsPrimaryCaregiverAsync(residentId, accountId, today);

    public Task<List<int>> PrimaryResidentIdsAsync(int accountId, DateOnly today) => Dao.PrimaryResidentIdsAsync(accountId, today);

    // S3-V4 nghỉ phép/đổi ca & bàn giao ca
    public Task<List<LeaveRequest>> ListLeaveRequestsAsync(int? involvingAccountId, LeaveRequestStatus? status, int take) =>
        Dao.ListLeaveRequestsAsync(involvingAccountId, status, take);

    public Task<LeaveRequest?> GetLeaveRequestAsync(int id) => Dao.GetLeaveRequestAsync(id);

    public Task<bool> HasPendingSwapAsync(int assignmentId) => Dao.HasPendingSwapAsync(assignmentId);

    public Task<bool> HasPendingLeaveOverlapAsync(int accountId, DateOnly from, DateOnly to) => Dao.HasPendingLeaveOverlapAsync(accountId, from, to);

    public Task<LeaveRequest> AddLeaveRequestAsync(LeaveRequest request) => Dao.AddLeaveRequestAsync(request);

    public Task SetLeaveStatusIfPendingAsync(int id, LeaveRequestStatus status, int? reviewerId, DateTime now, string? note) =>
        Dao.SetLeaveStatusIfPendingAsync(id, status, reviewerId, now, note);

    public Task<List<LeaveRequest>> ApproveSwapAsync(int id, int reviewerId, DateTime now, string? note) =>
        Dao.ApproveSwapAsync(id, reviewerId, now, note);

    public Task<(List<ShiftAssignment> Removed, List<LeaveRequest> Cancelled)> ApproveLeaveAsync(
        int id, int reviewerId, DateTime now, string? note, Func<ShiftAssignment, bool> canRemove) =>
        Dao.ApproveLeaveAsync(id, reviewerId, now, note, canRemove);

    public Task<bool> HandoverExistsAsync(DateOnly workDate, int shiftId, int fromAccountId) => Dao.HandoverExistsAsync(workDate, shiftId, fromAccountId);

    public Task<ShiftHandover> AddHandoverAsync(ShiftHandover handover) => Dao.AddHandoverAsync(handover);

    public Task<ShiftHandover?> GetHandoverAsync(long id) => Dao.GetHandoverAsync(id);

    public Task<List<ShiftHandover>> ListHandoversAsync(DateOnly from, DateOnly to, bool unreadOnly) => Dao.ListHandoversAsync(from, to, unreadOnly);

    public Task<bool> AcknowledgeHandoverAsync(long id, int accountId, DateTime now) => Dao.AcknowledgeHandoverAsync(id, accountId, now);

    public Task<HandoverDraftData> GetHandoverDraftDataAsync(DateTime startUtc, DateTime endUtc, IReadOnlyCollection<int>? residentIds) =>
        Dao.GetHandoverDraftDataAsync(startUtc, endUtc, residentIds);
}
