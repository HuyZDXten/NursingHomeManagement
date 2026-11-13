using NursingHome.BusinessObjects.Entities;

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

    Task DeleteAssignmentAsync(int id);

    Task<(List<ShiftAssignment> Added, int Skipped)> CopyWeekAsync(DateOnly fromMonday, DateOnly toMonday, DateOnly earliest, int byAccountId, DateTime now);

    Task<List<ResidentCareAssignment>> ListCareAssignmentsAsync(int? residentId, int? accountId, bool activeOnly);

    Task<ResidentCareAssignment?> GetCareAssignmentAsync(int id);

    Task<ResidentCareAssignment> AddCareAssignmentAsync(ResidentCareAssignment assignment);

    Task EndCareAssignmentAsync(int id, DateOnly endDate);

    Task<bool> IsPrimaryCaregiverAsync(int residentId, int accountId, DateOnly today);

    Task<List<int>> PrimaryResidentIdsAsync(int accountId, DateOnly today);
}
