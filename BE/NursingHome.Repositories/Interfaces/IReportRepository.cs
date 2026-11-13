using NursingHome.DataAccessObjects;

namespace NursingHome.Repositories.Interfaces;

/// <summary>MODULE VẬN HÀNH (Viên) — S3-V3 báo cáo (chỉ đọc).</summary>
public interface IReportRepository
{
    Task<List<RevenueRow>> MonthlyRevenueAsync(int year);

    Task<List<OccupancyRow>> OccupancyByMonthAsync(int year);

    Task<List<AdherenceRow>> MedicationAdherenceAsync(DateOnly from, DateOnly to);

    Task<List<DebtRow>> OutstandingDebtsAsync();
}
