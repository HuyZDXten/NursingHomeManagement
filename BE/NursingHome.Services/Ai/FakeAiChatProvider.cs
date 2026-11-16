using System.Text.Json;
using System.Text.RegularExpressions;

namespace NursingHome.Services.Ai;

/// <summary>
/// Provider giả (Ai:Provider = "Fake"): không gọi mạng, đủ cho unit test, smoke test và demo không có key.
/// <list type="bullet">
/// <item>Tin cuối là user chứa <c>/tool tên {json}</c> → gọi đúng tool đó (dùng trong test).</item>
/// <item>Câu hỏi y tế (thuốc, liều, bệnh…) → từ chối tư vấn, khuyên hỏi bác sĩ.</item>
/// <item>Từ khóa "giá/gói", "giường", "giờ thăm/quy trình", "tham quan" → gọi tool tương ứng nếu kênh có tool đó.</item>
/// <item>Tin cuối là kết quả tool → trả trường <c>summary</c> của JSON (tool nên luôn có), không thì nói đã tra cứu.</item>
/// </list>
/// </summary>
public sealed partial class FakeAiChatProvider : IAiChatProvider
{
    public const string Greeting =
        "Xin chào! Tôi là trợ lý thử nghiệm (chế độ Fake, không dùng AI thật). Bạn có thể hỏi về giá gói dịch vụ, giường trống, giờ thăm hoặc đặt lịch tham quan.";

    public const string MedicalRefusal =
        "Tôi không tư vấn chẩn đoán hay thuốc men. Gia đình vui lòng trao đổi trực tiếp với bác sĩ hoặc điều dưỡng của cơ sở, ví dụ khi đến tham quan.";

    private static readonly string[] MedicalWords = ["thuốc", "liều", "bệnh", "tiểu đường", "huyết áp", "chẩn đoán", "điều trị"];

    private static readonly (string[] Keywords, string Tool)[] KeywordTools =
    [
        (["giá", "gói", "chi phí", "bao nhiêu tiền"], "list_packages"),
        (["giường", "còn chỗ", "phòng trống"], "get_bed_availability"),
        (["tham quan", "lịch trống", "đặt lịch"], "get_tour_slots"),
        (["giờ thăm", "quy trình", "thủ tục", "nhập viện", "thanh toán", "quy định"], "search_knowledge"),
        // S4-V2 kênh nhân viên (hỏi đáp báo cáo)
        (["doanh thu", "thu được", "công nợ"], "revenue"),
        (["công suất", "lấp đầy"], "occupancy"),
        (["tuân thủ", "uống thuốc", "bỏ liều"], "medication_adherence")
    ];

    public string Name => AiOptions.FakeProvider;

    public Task<AiChatResult> CompleteAsync(AiChatRequest request, CancellationToken cancellationToken = default)
    {
        var last = request.Messages.LastOrDefault();
        if (last is null)
        {
            return Text(Greeting);
        }

        if (last.Role == "tool")
        {
            return Text(SummaryOf(last.Content));
        }

        var available = request.Tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);
        var directive = ToolDirective().Match(last.Content);
        if (directive.Success && available.Contains(directive.Groups["name"].Value))
        {
            var json = directive.Groups["json"].Success ? directive.Groups["json"].Value : "{}";
            return Call(directive.Groups["name"].Value, json);
        }

        // Tool của kênh hiện tại xét trước (kênh nhân viên có "tỉ lệ uống thuốc"), sau đó mới từ chối câu hỏi y tế.
        var lower = last.Content.ToLowerInvariant();
        foreach (var (keywords, tool) in KeywordTools)
        {
            if (available.Contains(tool) && keywords.Any(lower.Contains))
            {
                return Call(tool, tool == "search_knowledge" ? JsonSerializer.Serialize(new { query = last.Content }) : "{}");
            }
        }

        return Text(MedicalWords.Any(lower.Contains) ? MedicalRefusal : Greeting);
    }

    private static string SummaryOf(string toolJson)
    {
        try
        {
            using var document = JsonDocument.Parse(toolJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (document.RootElement.TryGetProperty("summary", out var summary) && summary.ValueKind == JsonValueKind.String)
                {
                    return summary.GetString()!;
                }

                if (document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                {
                    return $"Xin lỗi, tôi chưa làm được việc này: {error.GetString()}";
                }
            }
        }
        catch (JsonException)
        {
            // Kết quả tool không phải JSON — trả câu chung.
        }

        return "Tôi đã tra cứu dữ liệu của cơ sở nhưng chưa tóm tắt được. Bạn vui lòng gọi hotline để được hỗ trợ.";
    }

    private static Task<AiChatResult> Text(string text) => Task.FromResult(new AiChatResult(text, [], 0, 0));

    private static Task<AiChatResult> Call(string tool, string json) =>
        Task.FromResult(new AiChatResult(null, [new AiToolCall($"local-{Guid.NewGuid():N}", tool, JsonDocument.Parse(json).RootElement.Clone())], 0, 0));

    [GeneratedRegex(@"/tool\s+(?<name>[a-z_]+)(\s+(?<json>\{.*\}))?", RegexOptions.Singleline)]
    private static partial Regex ToolDirective();
}
