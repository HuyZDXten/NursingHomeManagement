using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Reports;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>
/// S3-V3 — UC-123 doanh thu &amp; công nợ, UC-122 công suất giường, UC-124 tuân thủ thuốc (UC-125 sự cố: sau S3-H1).
/// Số liệu lấy từ thủ tục SQL (Database/03_StoredProcedures.sql); service chỉ ghép đủ 12 tháng, tính %, ngày quá hạn.
/// </summary>
public class ReportService(IReportRepository reports, FacilityClock clock) : IReportService
{
    private const int MaxAdherenceDays = 366;

    private static readonly IReadOnlyDictionary<string, string> InvoiceStatusLabels = new Dictionary<string, string>
    {
        ["Unpaid"] = "Chưa thanh toán",
        ["PartiallyPaid"] = "Trả một phần"
    };

    // ---------------- UC-123 doanh thu & công nợ ----------------

    public async Task<RevenueReportDto> GetRevenueAsync(int? year)
    {
        var y = ValidYear(year);
        var collected = (await reports.MonthlyRevenueAsync(y)).ToDictionary(row => row.Month);
        var months = Enumerable.Range(1, 12).Select(month =>
        {
            var row = collected.GetValueOrDefault(month);
            return new RevenueMonthDto
            {
                Month = month,
                Collected = row?.Collected ?? 0,
                CollectedOnline = row?.CollectedOnline ?? 0,
                CollectedManual = (row?.Collected ?? 0) - (row?.CollectedOnline ?? 0),
                PaymentCount = row?.PaymentCount ?? 0
            };
        }).ToList();

        var today = clock.Today;
        var debts = (await reports.OutstandingDebtsAsync())
            .Select(debt => new DebtRowDto
            {
                InvoiceId = debt.InvoiceId,
                Number = debt.Number,
                ResidentCode = debt.ResidentCode,
                ResidentName = debt.ResidentName,
                BillingPeriod = debt.BillingPeriod,
                DueDate = debt.DueDate,
                Status = debt.Status,
                Total = debt.Total,
                Paid = debt.Paid,
                Remaining = debt.Remaining,
                DaysOverdue = Math.Max(0, today.DayNumber - debt.DueDate.DayNumber)
            })
            .OrderByDescending(debt => debt.DaysOverdue).ThenByDescending(debt => debt.Remaining)
            .ToList();

        return new RevenueReportDto
        {
            Year = y,
            Months = months,
            TotalCollected = months.Sum(month => month.Collected),
            TotalDebt = debts.Sum(debt => debt.Remaining),
            Debts = debts
        };
    }

    public async Task<ExportFile> ExportRevenueAsync(int? year)
    {
        var report = await GetRevenueAsync(year);
        using var excel = new ExcelExporter()
            .AddSheet("Thực thu theo tháng", $"Doanh thu thực thu năm {report.Year}", report.Months,
            [
                new("Tháng", m => $"{m.Month:00}/{report.Year}"),
                new("Thực thu (đ)", m => m.Collected, ExcelExporter.MoneyFormat),
                new("Qua PayOS (đ)", m => m.CollectedOnline, ExcelExporter.MoneyFormat),
                new("Thu tại quầy / chuyển khoản (đ)", m => m.CollectedManual, ExcelExporter.MoneyFormat),
                new("Số lần thu", m => m.PaymentCount)
            ], $"Tổng thực thu: {report.TotalCollected:#,##0} đ")
            .AddSheet("Công nợ", "Hóa đơn còn nợ", report.Debts,
            [
                new("Số hóa đơn", d => d.Number),
                new("Mã cư dân", d => d.ResidentCode),
                new("Cư dân", d => d.ResidentName),
                new("Kỳ", d => $"{d.BillingPeriod:MM/yyyy}"),
                new("Hạn thanh toán", d => d.DueDate, ExcelExporter.DateFormat),
                new("Tổng (đ)", d => d.Total, ExcelExporter.MoneyFormat),
                new("Đã trả (đ)", d => d.Paid, ExcelExporter.MoneyFormat),
                new("Còn nợ (đ)", d => d.Remaining, ExcelExporter.MoneyFormat),
                new("Quá hạn (ngày)", d => d.DaysOverdue),
                new("Trạng thái", d => InvoiceStatusLabels.GetValueOrDefault(d.Status, d.Status))
            ], $"Tổng còn nợ: {report.TotalDebt:#,##0} đ");
        return new ExportFile(excel.ToBytes(), $"doanh-thu-{report.Year}.xlsx");
    }

    // ---------------- UC-122 công suất giường ----------------

    public async Task<OccupancyReportDto> GetOccupancyAsync(int? year)
    {
        var y = ValidYear(year);
        var rows = (await reports.OccupancyByMonthAsync(y)).ToDictionary(row => row.Month);
        var months = Enumerable.Range(1, 12).Select(month =>
        {
            var row = rows.GetValueOrDefault(month);
            var available = row?.AvailableBedDays ?? 0;
            var occupied = row?.OccupiedBedDays ?? 0;
            return new OccupancyMonthDto
            {
                Month = month,
                OccupiedBedDays = occupied,
                AvailableBedDays = available,
                Percent = Percent(occupied, available)
            };
        }).ToList();

        return new OccupancyReportDto
        {
            Year = y,
            Months = months,
            AveragePercent = Percent(months.Sum(m => m.OccupiedBedDays), months.Sum(m => m.AvailableBedDays))
        };
    }

    public async Task<ExportFile> ExportOccupancyAsync(int? year)
    {
        var report = await GetOccupancyAsync(year);
        using var excel = new ExcelExporter().AddSheet("Công suất giường", $"Công suất giường năm {report.Year}", report.Months,
        [
            new("Tháng", m => $"{m.Month:00}/{report.Year}"),
            new("Giường-ngày có người", m => m.OccupiedBedDays, "#,##0"),
            new("Giường-ngày khả dụng", m => m.AvailableBedDays, "#,##0"),
            new("Công suất (%)", m => m.Percent, ExcelExporter.PercentFormat)
        ], report.AveragePercent is { } average ? $"Trung bình tới hôm nay: {average:0.0}%" : null);
        return new ExportFile(excel.ToBytes(), $"cong-suat-giuong-{report.Year}.xlsx");
    }

    // ---------------- UC-124 tuân thủ thuốc ----------------

    public async Task<AdherenceReportDto> GetAdherenceAsync(DateOnly? from, DateOnly? to)
    {
        var end = to ?? clock.Today;
        var start = from ?? end.AddDays(-29);
        if (end < start || end.DayNumber - start.DayNumber >= MaxAdherenceDays)
        {
            throw new BusinessRuleException($"Khoảng ngày không hợp lệ (tối đa {MaxAdherenceDays} ngày).");
        }

        var rows = (await reports.MedicationAdherenceAsync(start, end)).Select(row =>
        {
            var total = row.Taken + row.Missed + row.Refused;
            return new AdherenceRowDto
            {
                ResidentId = row.ResidentId,
                Code = row.Code,
                FullName = row.FullName,
                Taken = row.Taken,
                Missed = row.Missed,
                Refused = row.Refused,
                Total = total,
                TakenRate = Percent(row.Taken, total) ?? 0
            };
        }).ToList();

        return new AdherenceReportDto
        {
            From = start,
            To = end,
            Rows = rows,
            OverallTakenRate = Percent(rows.Sum(r => r.Taken), rows.Sum(r => r.Total))
        };
    }

    public async Task<ExportFile> ExportAdherenceAsync(DateOnly? from, DateOnly? to)
    {
        var report = await GetAdherenceAsync(from, to);
        using var excel = new ExcelExporter().AddSheet("Tuân thủ thuốc",
            $"Tuân thủ dùng thuốc {report.From:dd/MM/yyyy} – {report.To:dd/MM/yyyy}", report.Rows,
            [
                new("Mã cư dân", r => r.Code),
                new("Cư dân", r => r.FullName),
                new("Đã dùng", r => r.Taken),
                new("Bỏ lỡ", r => r.Missed),
                new("Từ chối", r => r.Refused),
                new("Tổng liều", r => r.Total),
                new("Tỉ lệ đã dùng (%)", r => r.TakenRate, ExcelExporter.PercentFormat)
            ], "Dữ liệu thuốc trong hệ thống demo là mô phỏng.");
        return new ExportFile(excel.ToBytes(), $"tuan-thu-thuoc-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.xlsx");
    }

    // ---------------- Quy tắc (public static để unit test) ----------------

    /// <summary>% làm tròn 1 chữ số, tối đa 100; null khi mẫu số = 0.</summary>
    public static decimal? Percent(int part, int whole) =>
        whole <= 0 ? null : Math.Min(100m, Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero));

    private int ValidYear(int? year)
    {
        var current = clock.Today.Year;
        var y = year ?? current;
        return y < 2000 || y > current + 1 ? throw new BusinessRuleException("Năm báo cáo không hợp lệ.") : y;
    }
}
