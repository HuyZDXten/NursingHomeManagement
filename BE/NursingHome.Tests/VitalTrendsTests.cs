using NursingHome.BusinessObjects.Entities;
using NursingHome.Services.Helpers;
using NursingHome.Services.Options;

namespace NursingHome.Tests;

public class VitalTrendsTests
{
    private static readonly VitalAlertOptions DefaultLimits = new();

    private static VitalSign Vital(int systolic, int minutesOffset = 0, int heartRate = 75, decimal temp = 36.6m, decimal spo2 = 98m) => new()
    {
        ResidentId = 1,
        Systolic = systolic,
        Diastolic = 80,
        HeartRate = heartRate,
        Temperature = temp,
        SpO2 = spo2,
        RecordedAt = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc).AddMinutes(minutesOffset)
    };

    [Fact]
    public void Analyze_EmptyOrInsufficient_ReturnsEmptyWhenNormal()
    {
        Assert.Empty(VitalTrends.Analyze([], DefaultLimits));
        Assert.Empty(VitalTrends.Analyze([Vital(120, 0), Vital(125, 60)], DefaultLimits));
    }

    [Fact]
    public void Analyze_DetectsThresholdConsecutiveTrendAndZScore()
    {
        // Quy tắc 1: vượt ngưỡng
        var over = VitalTrends.Analyze([Vital(165, 0)], DefaultLimits);
        Assert.Contains(over, f => f.Rule == "Threshold" && f.Metric == "Systolic");

        // Quy tắc 2: 3 lần tăng liên tiếp và lần cuối lệch > 10% trung bình
        var trend = VitalTrends.Analyze([
            Vital(110, 0), Vital(115, 60), Vital(135, 120)
        ], DefaultLimits);
        Assert.Contains(trend, f => f.Rule == "ConsecutiveTrend" && f.Metric == "Systolic");

        // Quy tắc 3: >= 5 lần đo, |z-score| >= 2
        var zList = new List<VitalSign>
        {
            Vital(120, 0), Vital(121, 60), Vital(119, 120), Vital(120, 180), Vital(121, 240), Vital(150, 300)
        };
        var zFindings = VitalTrends.Analyze(zList, DefaultLimits);
        Assert.Contains(zFindings, f => f.Rule == "ZScore" && f.Metric == "Systolic");
    }
}
