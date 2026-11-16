namespace NursingHome.Services.Helpers;

/// <summary>Nhãn tiếng Việt của loại và mức độ sự cố (bảng Incidents của S3-H1), dùng cho báo cáo, tool AI và gợi ý bàn giao.</summary>
public static class IncidentLabels
{
    public static readonly IReadOnlyDictionary<string, string> Types = new Dictionary<string, string>
    {
        ["Fall"] = "Té ngã", ["Injury"] = "Chấn thương", ["Behavior"] = "Hành vi", ["Other"] = "Khác"
    };

    public static readonly IReadOnlyDictionary<string, string> Severities = new Dictionary<string, string>
    {
        ["Low"] = "Nhẹ", ["Medium"] = "Trung bình", ["High"] = "Nặng"
    };

    public static string Type(string code) => Types.TryGetValue(code, out var label) ? label : code;

    public static string Severity(string code) => Severities.TryGetValue(code, out var label) ? label : code;
}
