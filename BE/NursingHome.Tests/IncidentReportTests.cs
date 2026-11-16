using NSubstitute;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DataAccessObjects;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Ai.Tools;
using NursingHome.Services.Helpers;
using NursingHome.Services.Implementations;
using NursingHome.Services.Interfaces;

namespace NursingHome.Tests;

/// <summary>UC-125 báo cáo sự cố, tool AI incident_summary, ngưỡng sinh hiệu riêng trong gợi ý bàn giao ca.</summary>
public class IncidentReportTests
{
    private static IncidentSummaryRow Row(int year, int month, string type, string severity, int total, int closed = 0) =>
        new(year, month, type, severity, total, closed);

    [Fact]
    public void Report_FillsEveryMonthTypeAndSeverityWithZero()
    {
        var report = ReportService.BuildIncidentReport(new DateOnly(2026, 9, 15), new DateOnly(2026, 11, 20),
        [
            Row(2026, 9, "Fall", "Medium", 2, 1),
            Row(2026, 11, "Behavior", "Low", 1)
        ]);

        Assert.Equal(["09/2026", "10/2026", "11/2026"], report.Months.Select(month => month.Label));
        Assert.Equal([2, 0, 1], report.Months.Select(month => month.Total));
        Assert.Equal(0, report.Months[1].ByType["Fall"]);
        Assert.Equal(0, report.Months[1].BySeverity["High"]);
        Assert.Equal(4, report.Months[0].ByType.Count);
        Assert.Equal(3, report.Months[0].BySeverity.Count);
        Assert.Equal(3, report.Total);
        Assert.Equal(1, report.Closed);
        Assert.Equal(33.3m, report.ClosedRate);
        Assert.Equal(2, report.TotalByType["Fall"]);
        Assert.Equal(1, report.TotalBySeverity["Low"]);
    }

    [Fact]
    public void Report_SumsRowsOfTheSameMonthAndHandlesNoIncidents()
    {
        var merged = ReportService.BuildIncidentReport(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30),
            [Row(2026, 11, "Fall", "Low", 1, 1), Row(2026, 11, "Fall", "High", 2), Row(2026, 11, "Injury", "High", 1, 1)]);
        var month = Assert.Single(merged.Months);
        Assert.Equal(4, month.Total);
        Assert.Equal(3, month.ByType["Fall"]);
        Assert.Equal(3, month.BySeverity["High"]);
        Assert.Equal(2, month.Closed);

        var empty = ReportService.BuildIncidentReport(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), []);
        Assert.Equal(0, empty.Total);
        Assert.Null(empty.ClosedRate);
    }

    [Fact]
    public void Report_UnknownTypeStillCountsInTotals()
    {
        // Dữ liệu ngoài danh mục (không xảy ra nhờ CHECK) không được làm tổng lệch bảng Incidents.
        var report = ReportService.BuildIncidentReport(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), [Row(2026, 11, "Mystery", "Low", 2)]);
        Assert.Equal(2, report.Total);
        Assert.Equal(0, report.TotalByType.Values.Sum());
    }

    [Fact]
    public async Task Report_RejectsInvertedOrTooLongRange()
    {
        var service = new ReportService(Substitute.For<IReportRepository>(), new FacilityClock(TimeProvider.System));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetIncidentsAsync(new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 1)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetIncidentsAsync(new DateOnly(2024, 1, 1), new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void Labels_FallBackToCodeForUnknownValues()
    {
        Assert.Equal("Té ngã", IncidentLabels.Type("Fall"));
        Assert.Equal("Nặng", IncidentLabels.Severity("High"));
        Assert.Equal("Lạ", IncidentLabels.Type("Lạ"));
    }

    // ---------------- Tool AI ----------------

    private static CurrentUser User(string role) => new(1, role, "t@example.com", "Tester");

    private static IReportService ReportsWithOneIncident()
    {
        var reports = Substitute.For<IReportService>();
        reports.GetIncidentsAsync(null, null).Returns(ReportService.BuildIncidentReport(new DateOnly(2026, 9, 1), new DateOnly(2026, 11, 20),
            [Row(2026, 10, "Fall", "High", 2, 1), Row(2026, 11, "Behavior", "Low", 1)]));
        return reports;
    }

    [Fact]
    public async Task Tool_AnswersFromReportForAdminAndNurse()
    {
        var tool = new IncidentTool(ReportsWithOneIncident());
        var args = System.Text.Json.JsonDocument.Parse("{}").RootElement;
        foreach (string role in new[] { Roles.Admin, Roles.Nurse })
        {
            using var result = System.Text.Json.JsonDocument.Parse(await tool.ExecuteAsync(User(role), args));
            string summary = result.RootElement.GetProperty("summary").GetString()!;
            Assert.Contains("3 sự cố", summary);
            Assert.Contains("té ngã 2", summary);
            Assert.Contains("2 nặng", summary);
            Assert.Contains("Nhiều nhất tháng 10/2026", summary);
            Assert.Equal(3, result.RootElement.GetProperty("total").GetInt32());
        }
    }

    [Theory]
    [InlineData(Roles.Accountant)]
    [InlineData(Roles.Doctor)]
    [InlineData(Roles.Receptionist)]
    [InlineData(Roles.Caregiver)]
    [InlineData(Roles.Family)]
    public async Task Tool_KeepsReportPermission(string role)
    {
        var reports = ReportsWithOneIncident();
        var tool = new IncidentTool(reports);
        await Assert.ThrowsAsync<ForbiddenException>(() => tool.ExecuteAsync(User(role), System.Text.Json.JsonDocument.Parse("{}").RootElement));
        await reports.DidNotReceiveWithAnyArgs().GetIncidentsAsync(default, default);
    }

    // ---------------- Ngưỡng sinh hiệu riêng trong bàn giao ca ----------------

    [Fact]
    public void Handover_UsesResidentThresholdWhenSet()
    {
        Assert.Null(HandoverService.ToLimits(null));
        var limits = HandoverService.ToLimits(new VitalThreshold
        {
            SystolicHigh = 180, SystolicLow = 100, HeartRateHigh = 110, HeartRateLow = 55, TemperatureHigh = 37.8m, SpO2Low = 94m
        });
        Assert.NotNull(limits);
        Assert.Equal(180, limits.SystolicHigh);
        Assert.Equal(55, limits.HeartRateLow);
        Assert.Equal(37.8m, limits.TemperatureHigh);
        Assert.Equal(94m, limits.SpO2Low);
    }
}
