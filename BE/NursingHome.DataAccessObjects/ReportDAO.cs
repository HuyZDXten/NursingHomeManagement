using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects;

namespace NursingHome.DataAccessObjects;

// Kết quả thô của thủ tục/view báo cáo (Database/03_StoredProcedures.sql, 02_Views_Functions.sql).
// SqlQuery<T> yêu cầu tên cột trả về khớp tên thuộc tính.

public sealed record RevenueRow(int Month, decimal Collected, decimal CollectedOnline, int PaymentCount);

public sealed record OccupancyRow(int Month, int AvailableBedDays, int OccupiedBedDays);

public sealed record AdherenceRow(int ResidentId, string Code, string FullName, int Taken, int Missed, int Refused);

public sealed record DebtRow(
    int InvoiceId,
    string Number,
    string ResidentCode,
    string ResidentName,
    DateOnly BillingPeriod,
    DateOnly DueDate,
    string Status,
    decimal Total,
    decimal Paid,
    decimal Remaining);

/// <summary>
/// MODULE VẬN HÀNH (Viên) — S3-V3 báo cáo. Chỉ đọc; tham số nội suy trong SqlQuery được EF đổi thành SqlParameter
/// (không ghép chuỗi SQL từ input).
/// </summary>
public sealed class ReportDAO
{
    public static ReportDAO Instance { get; } = new();

    private ReportDAO()
    {
    }

    public async Task<List<RevenueRow>> MonthlyRevenueAsync(int year)
    {
        await using var context = new NursingHomeContext();
        return await context.Database.SqlQuery<RevenueRow>($"EXEC dbo.sp_MonthlyRevenue @Year = {year}").ToListAsync();
    }

    public async Task<List<OccupancyRow>> OccupancyByMonthAsync(int year)
    {
        await using var context = new NursingHomeContext();
        return await context.Database.SqlQuery<OccupancyRow>($"EXEC dbo.sp_OccupancyByMonth @Year = {year}").ToListAsync();
    }

    public async Task<List<AdherenceRow>> MedicationAdherenceAsync(DateOnly from, DateOnly to)
    {
        await using var context = new NursingHomeContext();
        return await context.Database
            .SqlQuery<AdherenceRow>($"EXEC dbo.sp_MedicationAdherence @From = {from}, @To = {to}")
            .ToListAsync();
    }

    /// <summary>Hóa đơn đã phát hành còn nợ (vw_InvoiceBalances).</summary>
    public async Task<List<DebtRow>> OutstandingDebtsAsync()
    {
        await using var context = new NursingHomeContext();
        return await context.Database.SqlQuery<DebtRow>($"""
            SELECT b.InvoiceId, b.Number, r.Code AS ResidentCode, r.FullName AS ResidentName, b.BillingPeriod, b.DueDate,
                   b.Status, CAST(b.Total AS DECIMAL(18, 0)) AS Total, CAST(b.Paid AS DECIMAL(18, 0)) AS Paid,
                   CAST(b.Remaining AS DECIMAL(18, 0)) AS Remaining
            FROM dbo.vw_InvoiceBalances AS b
            JOIN dbo.Residents AS r ON r.Id = b.ResidentId
            WHERE b.Remaining > 0 AND b.Status IN (N'Unpaid', N'PartiallyPaid')
            """).ToListAsync();
    }
}
