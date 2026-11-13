using System.Globalization;
using System.Text.Json;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Common;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Ai.Tools;

// ======================================================================================================
// S4-V2 — AI-12: tool hỏi đáp báo cáo cho nhân viên. Gọi IReportService có sẵn (số liệu SQL thật), KHÔNG sinh SQL.
// IReportService không nhận CurrentUser nên mỗi tool tự kiểm role giống policy của ReportsController.
// ======================================================================================================

public static class ReportTools
{
    public static IReadOnlyList<IAiTool> Create(IReportService reports) =>
        [new RevenueTool(reports), new OccupancyTool(reports), new AdherenceTool(reports)];

    internal static void Require(CurrentUser? user, params string[] roles)
    {
        if (user is null || !user.IsInRole(roles))
        {
            throw new ForbiddenException("Bạn không có quyền xem báo cáo này.");
        }
    }

    internal static string Percent(decimal? value) => value is null ? "—" : $"{value.Value.ToString("0.#", CultureInfo.GetCultureInfo("vi-VN"))}%";
}

public sealed class RevenueTool(IReportService reports) : IAiTool
{
    public AiToolDefinition Definition { get; } = new("revenue",
        "Doanh thu thực thu theo tháng của một năm (tách PayOS/thủ công) và tổng công nợ hiện tại.",
        AiToolJson.Schema(("year", "integer", "Năm, ví dụ 2026 (mặc định năm nay)", false)));

    public async Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        ReportTools.Require(user, Roles.Admin, Roles.Accountant); // policy FinanceStaff
        var report = await reports.GetRevenueAsync(AiToolJson.Int(arguments, "year"));
        var best = report.Months.MaxBy(month => month.Collected);
        return AiToolJson.Serialize(new
        {
            report.Year,
            months = report.Months.Select(month => new { month.Month, month.Collected, month.CollectedOnline, month.CollectedManual }),
            report.TotalCollected,
            report.TotalDebt,
            debtCount = report.Debts.Count,
            summary = $"Năm {report.Year}: tổng thực thu {PublicTools.Money(report.TotalCollected)}"
                + (best is { Collected: > 0 } ? $", cao nhất tháng {best.Month} ({PublicTools.Money(best.Collected)})" : "")
                + $". Công nợ hiện tại {PublicTools.Money(report.TotalDebt)} ({report.Debts.Count} hóa đơn)."
        });
    }
}

public sealed class OccupancyTool(IReportService reports) : IAiTool
{
    public AiToolDefinition Definition { get; } = new("occupancy",
        "Công suất giường (%) theo tháng của một năm = giường-ngày có người / giường-ngày khả dụng.",
        AiToolJson.Schema(("year", "integer", "Năm, ví dụ 2026 (mặc định năm nay)", false)));

    public async Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        ReportTools.Require(user, Roles.Admin, Roles.Receptionist, Roles.Accountant); // policy BillingStaff
        var report = await reports.GetOccupancyAsync(AiToolJson.Int(arguments, "year"));
        return AiToolJson.Serialize(new
        {
            report.Year,
            months = report.Months.Select(month => new { month.Month, month.Percent }),
            report.AveragePercent,
            summary = $"Công suất giường năm {report.Year}: trung bình {ReportTools.Percent(report.AveragePercent)}; "
                + string.Join(", ", report.Months.Where(month => month.Percent is not null).Select(month => $"T{month.Month} {ReportTools.Percent(month.Percent)}")) + "."
        });
    }
}

public sealed class AdherenceTool(IReportService reports) : IAiTool
{
    public AiToolDefinition Definition { get; } = new("medication_adherence",
        "Tuân thủ dùng thuốc theo cư dân trong khoảng ngày (số liều đã uống/bỏ lỡ/từ chối, tỉ lệ đã uống). Mặc định 30 ngày gần nhất.",
        AiToolJson.Schema(("from", "string", "Từ ngày yyyy-MM-dd (tùy chọn)", false), ("to", "string", "Đến ngày yyyy-MM-dd (tùy chọn)", false)));

    public async Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        ReportTools.Require(user, Roles.Admin, Roles.Doctor); // policy Prescribers
        DateOnly? from = DateOnly.TryParse(AiToolJson.String(arguments, "from"), CultureInfo.InvariantCulture, out var start) ? start : null;
        DateOnly? to = DateOnly.TryParse(AiToolJson.String(arguments, "to"), CultureInfo.InvariantCulture, out var end) ? end : null;
        var report = await reports.GetAdherenceAsync(from, to);
        var lowest = report.Rows.Where(row => row.Total > 0).OrderBy(row => row.TakenRate).FirstOrDefault();
        return AiToolJson.Serialize(new
        {
            from = report.From.ToString("yyyy-MM-dd"),
            to = report.To.ToString("yyyy-MM-dd"),
            rows = report.Rows.Select(row => new { row.Code, row.Taken, row.Missed, row.Refused, row.TakenRate }),
            report.OverallTakenRate,
            summary = $"Từ {report.From:dd/MM} đến {report.To:dd/MM/yyyy}: tỉ lệ đã uống chung {ReportTools.Percent(report.OverallTakenRate)}"
                + (lowest is null ? "." : $", thấp nhất {lowest.Code} ({ReportTools.Percent(lowest.TakenRate)}).")
        });
    }
}
