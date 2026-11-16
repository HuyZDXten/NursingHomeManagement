using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;

namespace NursingHome.Repositories.Interfaces;

/// <summary>MODULE VẬN HÀNH (Viên) — S3-V2 ca trực &amp; phân công phụ trách.</summary>
public interface IStaffingRepository
{
    Task<List<Shift>> ListShiftsAsync(bool activeOnly);

    Task<Shift?> GetShiftAsync(int id);

    Task<List<ShiftAssignment>> ListAssignmentsAsync(DateOnly from, DateOnly to, int? accountId);

    Task<ShiftAssignment?> GetAssignmentAsync(int id);

    Task<bool> AssignmentExistsAsync(int accountId, DateOnly workDate, int shiftId);

    Task<ShiftAssignment> AddAssignmentAsync(ShiftAssignment assignment);

    Task<List<LeaveRequest>> DeleteAssignmentAsync(int id, DateTime now);

    Task<(List<ShiftAssignment> Added, int Skipped)> CopyWeekAsync(DateOnly fromMonday, DateOnly toMonday, DateOnly earliest, int byAccountId, DateTime now);

    Task<List<ResidentCareAssignment>> ListCareAssignmentsAsync(int? residentId, int? accountId, bool activeOnly);

    Task<ResidentCareAssignment?> GetCareAssignmentAsync(int id);

    Task<ResidentCareAssignment> AddCareAssignmentAsync(ResidentCareAssignment assignment);

    Task EndCareAssignmentAsync(int id, DateOnly endDate);

    Task<bool> IsPrimaryCaregiverAsync(int residentId, int accountId, DateOnly today);

    Task<List<int>> PrimaryResidentIdsAsync(int accountId, DateOnly today);

    // S3-V4 nghỉ phép/đổi ca & bàn giao ca
    Task<List<LeaveRequest>> ListLeaveRequestsAsync(int? involvingAccountId, LeaveRequestStatus? status, int take);

    Task<LeaveRequest?> GetLeaveRequestAsync(int id);

    Task<bool> HasPendingSwapAsync(int assignmentId);

    Task<bool> HasPendingLeaveOverlapAsync(int accountId, DateOnly from, DateOnly to);

    Task<LeaveRequest> AddLeaveRequestAsync(LeaveRequest request);

    Task SetLeaveStatusIfPendingAsync(int id, LeaveRequestStatus status, int? reviewerId, DateTime now, string? note);

    Task<List<LeaveRequest>> ApproveSwapAsync(int id, int reviewerId, DateTime now, string? note);

    Task<(List<ShiftAssignment> Removed, List<LeaveRequest> Cancelled)> ApproveLeaveAsync(
        int id, int reviewerId, DateTime now, string? note, Func<ShiftAssignment, bool> canRemove);

    Task<bool> HandoverExistsAsync(DateOnly workDate, int shiftId, int fromAccountId);

    Task<ShiftHandover> AddHandoverAsync(ShiftHandover handover);

    Task<ShiftHandover?> GetHandoverAsync(long id);

    Task<List<ShiftHandover>> ListHandoversAsync(DateOnly from, DateOnly to, bool unreadOnly);

    Task<bool> AcknowledgeHandoverAsync(long id, int accountId, DateTime now);

    Task<HandoverDraftData> GetHandoverDraftDataAsync(DateTime startUtc, DateTime endUtc, IReadOnlyCollection<int>? residentIds);
}
