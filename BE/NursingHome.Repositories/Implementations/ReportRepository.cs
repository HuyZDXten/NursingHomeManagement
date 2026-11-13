using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class ReportRepository : IReportRepository
{
    private static ReportDAO Dao => ReportDAO.Instance;

    public Task<List<RevenueRow>> MonthlyRevenueAsync(int year) => Dao.MonthlyRevenueAsync(year);

    public Task<List<OccupancyRow>> OccupancyByMonthAsync(int year) => Dao.OccupancyByMonthAsync(year);

    public Task<List<AdherenceRow>> MedicationAdherenceAsync(DateOnly from, DateOnly to) => Dao.MedicationAdherenceAsync(from, to);

    public Task<List<DebtRow>> OutstandingDebtsAsync() => Dao.OutstandingDebtsAsync();
}
