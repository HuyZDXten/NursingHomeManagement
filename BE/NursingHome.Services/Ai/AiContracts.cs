using System.Text.Json;
using System.Text.Json.Nodes;
using NursingHome.DTOs.Common;

namespace NursingHome.Services.Ai;

// ======================================================================================================
// HỢP ĐỒNG AI DÙNG CHUNG (S4-V1, chốt cho cả nhóm). Huy viết tool kênh Family theo IAiTool và gọi IAiChatEngine.
// ======================================================================================================

/// <summary>
/// Một tin trong hội thoại gửi cho model. Role: user | assistant | tool.
/// <para><see cref="Native"/>: nội dung gốc của provider cho lượt assistant gọi tool (Gemini 3 cần gửi lại nguyên văn,
/// gồm thoughtSignature). Chỉ dùng trong cùng một request, không lưu DB.</para>
/// </summary>
public sealed record AiMessage(string Role, string Content, string? ToolName = null, string? ToolCallId = null, JsonElement? Native = null);

/// <summary><paramref name="ParametersSchema"/>: JSON Schema của tham số (type object, properties, required).</summary>
public sealed record AiToolDefinition(string Name, string Description, JsonObject ParametersSchema);

/// <summary><paramref name="Id"/> bắt đầu bằng "local-" khi provider không cấp id (khi đó không gửi lại id cho provider).</summary>
public sealed record AiToolCall(string Id, string Name, JsonElement Arguments);

public sealed record AiChatRequest(string SystemPrompt, IReadOnlyList<AiMessage> Messages, IReadOnlyList<AiToolDefinition> Tools);

/// <summary><paramref name="Native"/>: nội dung gốc lượt trả lời của provider (để gửi lại khi có tool call).</summary>
public sealed record AiChatResult(string? Text, IReadOnlyList<AiToolCall> ToolCalls, int InputTokens, int OutputTokens, JsonElement? Native = null);

public interface IAiChatProvider
{
    /// <summary>Tên hiển thị trong log ("Gemini", "Fake").</summary>
    string Name { get; }

    Task<AiChatResult> CompleteAsync(AiChatRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Một tool AI. Trả về JSON (chuỗi). PHẢI kiểm quyền bằng service có sẵn với user được truyền vào.</summary>
public interface IAiTool
{
    AiToolDefinition Definition { get; }

    Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default);
}

public sealed record AiReply(string Text, long ConversationId);

public interface IAiChatEngine
{
    /// <summary>
    /// Một lượt hỏi–đáp: nạp 20 tin gần nhất, gọi model, chạy tool (tối đa 3 vòng), lưu tin + token.
    /// Lỗi provider → câu xin lỗi lịch sự (không ném 500).
    /// </summary>
    Task<AiReply> ChatAsync(string channel, CurrentUser? user, string? anonymousKey, string message,
        string systemPrompt, IReadOnlyList<IAiTool> tools, CancellationToken cancellationToken = default);

    /// <summary>Lịch sử user/assistant của hội thoại (không gồm kết quả tool) để hiện lại khung chat.</summary>
    Task<List<AiMessage>> HistoryAsync(string channel, int? accountId, string? anonymousKey, int take = 50);
}

/// <summary>Provider không trả lời được (cấu hình thiếu, bị chặn, hết thời gian…). Engine bắt và trả câu xin lỗi.</summary>
public sealed class AiProviderException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Tiện ích cho tool: đọc tham số an toàn, trả JSON camelCase.</summary>
public static class AiToolJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(object value) => JsonSerializer.Serialize(value, Options);

    public static string? String(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static int? Int(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt32(out var number) => number,
                JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
                _ => null
            }
            : null;

    public static bool Bool(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var value)
        && (value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.String && value.GetString() == "true"));

    /// <summary>Schema object đơn giản: (tên, kiểu JSON, mô tả, bắt buộc).</summary>
    public static JsonObject Schema(params (string Name, string Type, string Description, bool Required)[] properties)
    {
        var props = new JsonObject();
        foreach (var (name, type, description, _) in properties)
        {
            props[name] = new JsonObject { ["type"] = type, ["description"] = description };
        }

        var schema = new JsonObject { ["type"] = "object", ["properties"] = props };
        var required = properties.Where(item => item.Required).Select(item => (JsonNode?)JsonValue.Create(item.Name)).ToArray();
        if (required.Length > 0)
        {
            schema["required"] = new JsonArray(required);
        }

        return schema;
    }
}
