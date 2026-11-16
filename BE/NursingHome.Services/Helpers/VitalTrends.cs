using NursingHome.BusinessObjects.Entities;
using NursingHome.Services.Options;

namespace NursingHome.Services.Helpers;

public sealed record TrendFinding(string Rule, string Metric, string Detail);

/// <summary>
/// S4-H2 (AI-06): phát hiện bất thường sinh hiệu 14 ngày bằng thống kê thuần C#.
/// AI chỉ diễn giải lại các kết quả đã tính, không tự phát hiện.
/// </summary>
public static class VitalTrends
{
    public static List<TrendFinding> Analyze(IReadOnlyList<VitalSign> last14Days, VitalAlertOptions limits)
    {
        if (last14Days.Count == 0)
        {
            return [];
        }

        var ordered = last14Days.OrderBy(v => v.RecordedAt).ToList();
        var latest = ordered[^1];
        var findings = new List<TrendFinding>();

        // (1) Giá trị mới nhất vượt ngưỡng riêng/mặc định
        if (latest.Systolic >= limits.SystolicHigh || latest.Systolic <= limits.SystolicLow)
        {
            findings.Add(new("Threshold", "Systolic",
                $"Huyết áp tâm thu mới nhất {latest.Systolic} mmHg vượt ngưỡng ({limits.SystolicLow}–{limits.SystolicHigh})."));
        }
        if (latest.HeartRate >= limits.HeartRateHigh || latest.HeartRate <= limits.HeartRateLow)
        {
            findings.Add(new("Threshold", "HeartRate",
                $"Nhịp tim mới nhất {latest.HeartRate} l/p vượt ngưỡng ({limits.HeartRateLow}–{limits.HeartRateHigh})."));
        }
        if (latest.Temperature >= limits.TemperatureHigh)
        {
            findings.Add(new("Threshold", "Temperature",
                $"Nhiệt độ mới nhất {latest.Temperature}°C ≥ {limits.TemperatureHigh}°C."));
        }
        if (latest.SpO2.HasValue && latest.SpO2.Value < limits.SpO2Low)
        {
            findings.Add(new("Threshold", "SpO2",
                $"SpO2 mới nhất {latest.SpO2.Value}% < {limits.SpO2Low}%."));
        }

        // (2) 3 lần đo liên tiếp tăng (hoặc giảm) đều và lần cuối lệch > 10% trung bình
        CheckTrend(ordered.Select(v => (double)v.Systolic).ToList(), "Systolic", "Huyết áp tâm thu", "mmHg", findings);
        CheckTrend(ordered.Select(v => (double)v.HeartRate).ToList(), "HeartRate", "Nhịp tim", "l/p", findings);

        // (3) |z-score| >= 2 so với trung bình 14 ngày (cần >= 5 lần đo)
        if (ordered.Count >= 5)
        {
            CheckZScore(ordered.Select(v => (double)v.Systolic).ToList(), "Systolic", "Huyết áp tâm thu", "mmHg", findings);
            CheckZScore(ordered.Select(v => (double)v.HeartRate).ToList(), "HeartRate", "Nhịp tim", "l/p", findings);
            CheckZScore(ordered.Select(v => (double)v.Temperature).ToList(), "Temperature", "Nhiệt độ", "°C", findings);
        }

        return findings;
    }

    private static void CheckTrend(IReadOnlyList<double> values, string metric, string label, string unit, List<TrendFinding> findings)
    {
        if (values.Count < 3)
        {
            return;
        }

        double a = values[^3], b = values[^2], c = values[^1];
        bool increasing = a < b && b < c;
        bool decreasing = a > b && b > c;
        if (!increasing && !decreasing)
        {
            return;
        }

        double avg = values.Average();
        if (avg <= 0)
        {
            return;
        }

        double pct = Math.Abs(c - avg) / avg;
        if (pct > 0.10)
        {
            string dir = increasing ? "tăng" : "giảm";
            findings.Add(new("ConsecutiveTrend", metric,
                $"{label} {dir} liên tiếp 3 lần ({a:0.#} → {b:0.#} → {c:0.#} {unit}), lệch {pct * 100:0.#}% so với trung bình 14 ngày ({avg:0.#} {unit})."));
        }
    }

    private static void CheckZScore(IReadOnlyList<double> values, string metric, string label, string unit, List<TrendFinding> findings)
    {
        double mean = values.Average();
        double variance = values.Sum(x => Math.Pow(x - mean, 2)) / values.Count;
        double stdDev = Math.Sqrt(variance);
        if (stdDev <= 0.0001)
        {
            return;
        }

        double latest = values[^1];
        double z = Math.Abs((latest - mean) / stdDev);
        if (z >= 2.0)
        {
            findings.Add(new("ZScore", metric,
                $"{label} mới nhất ({latest:0.#} {unit}) lệch {z:0.#} độ lệch chuẩn so với trung bình 14 ngày ({mean:0.#} {unit})."));
        }
    }
}
