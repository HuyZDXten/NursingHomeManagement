using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace NursingHome.Services.Ai;

/// <summary>
/// Gemini generateContent (REST) có function calling, theo cách dựng payload của StreetBiz (GeminiChatbotModel):
/// systemInstruction + contents (user/model, functionCall/functionResponse) + tools.functionDeclarations (parametersJsonSchema).
/// Lượt model gọi tool được gửi lại NGUYÊN VĂN (<see cref="AiMessage.Native"/>) vì Gemini 3 kiểm thoughtSignature.
/// Không log key, không log prompt.
/// </summary>
public sealed class GeminiChatProvider(HttpClient http, IOptions<AiOptions> options) : IAiChatProvider
{
    public string Name => AiOptions.GeminiProvider;

    public async Task<AiChatResult> CompleteAsync(AiChatRequest request, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var gemini = settings.Gemini;
        if (string.IsNullOrWhiteSpace(gemini.ApiKey) || string.IsNullOrWhiteSpace(gemini.Model))
        {
            throw new AiProviderException("Chưa cấu hình Ai:Gemini:ApiKey / Ai:Gemini:Model.");
        }

        var payload = BuildPayload(request, gemini.Model, settings.MaxOutputTokens, settings.Temperature);
        using var message = new HttpRequestMessage(HttpMethod.Post,
            $"{gemini.BaseUrl.TrimEnd('/')}/models/{Uri.EscapeDataString(gemini.Model)}:generateContent")
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
        };
        message.Headers.Add("x-goog-api-key", gemini.ApiKey);

        using var response = await http.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new AiProviderException($"Gemini trả HTTP {(int)response.StatusCode}.");
        }

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return ParseResponse(document.RootElement);
    }

    /// <summary>Tách riêng để unit test payload không cần mạng.</summary>
    public static JsonObject BuildPayload(AiChatRequest request, string model, int maxOutputTokens, double temperature)
    {
        var contents = new JsonArray();
        JsonArray? pendingResponses = null;
        foreach (var item in request.Messages)
        {
            if (item.Role == "tool")
            {
                pendingResponses ??= [];
                var functionResponse = new JsonObject
                {
                    ["name"] = item.ToolName,
                    ["response"] = new JsonObject { ["result"] = ParseOrText(item.Content) }
                };
                if (item.ToolCallId is { } id && !id.StartsWith("local-", StringComparison.Ordinal))
                {
                    functionResponse["id"] = id;
                }

                pendingResponses.Add(new JsonObject { ["functionResponse"] = functionResponse });
                continue;
            }

            if (pendingResponses is not null)
            {
                contents.Add(new JsonObject { ["role"] = "user", ["parts"] = pendingResponses });
                pendingResponses = null;
            }

            if (item.Role == "assistant" && item.Native is { } native)
            {
                contents.Add(JsonNode.Parse(native.GetRawText()));
            }
            else
            {
                contents.Add(new JsonObject
                {
                    ["role"] = item.Role == "assistant" ? "model" : "user",
                    ["parts"] = new JsonArray(new JsonObject { ["text"] = item.Content })
                });
            }
        }

        if (pendingResponses is not null)
        {
            contents.Add(new JsonObject { ["role"] = "user", ["parts"] = pendingResponses });
        }

        var generation = new JsonObject { ["maxOutputTokens"] = maxOutputTokens };
        if (model.StartsWith("gemini-3", StringComparison.OrdinalIgnoreCase))
        {
            // Gemini 3: giữ độ trễ thấp, không yêu cầu/không trả phần "thought". Nhiệt độ để mặc định của model.
            generation["thinkingConfig"] = new JsonObject { ["thinkingLevel"] = "low", ["includeThoughts"] = false };
        }
        else
        {
            generation["temperature"] = temperature;
        }

        var payload = new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = request.SystemPrompt }) },
            ["contents"] = contents,
            ["generationConfig"] = generation
        };
        if (request.Tools.Count > 0)
        {
            var declarations = new JsonArray();
            foreach (var tool in request.Tools)
            {
                declarations.Add(new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parametersJsonSchema"] = tool.ParametersSchema.DeepClone()
                });
            }

            payload["tools"] = new JsonArray(new JsonObject { ["functionDeclarations"] = declarations });
        }

        return payload;
    }

    public static AiChatResult ParseResponse(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            throw new AiProviderException("Gemini không trả câu trả lời (có thể bị chặn bởi bộ lọc an toàn).");
        }

        var candidate = candidates[0];
        if (!candidate.TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts))
        {
            var reason = candidate.TryGetProperty("finishReason", out var finish) ? finish.GetString() : "UNKNOWN";
            throw new AiProviderException($"Gemini kết thúc không có nội dung ({reason}).");
        }

        var text = new StringBuilder();
        var calls = new List<AiToolCall>();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("functionCall", out var call))
            {
                var id = call.TryGetProperty("id", out var callId) && callId.GetString() is { Length: > 0 } given ? given : $"local-{Guid.NewGuid():N}";
                var args = call.TryGetProperty("args", out var arguments) ? arguments.Clone() : JsonDocument.Parse("{}").RootElement.Clone();
                calls.Add(new AiToolCall(id, call.GetProperty("name").GetString() ?? "", args));
            }
            else if (part.TryGetProperty("text", out var chunk) && !(part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True))
            {
                text.Append(chunk.GetString());
            }
        }

        var input = 0;
        var output = 0;
        if (root.TryGetProperty("usageMetadata", out var usage))
        {
            input = usage.TryGetProperty("promptTokenCount", out var prompt) ? prompt.GetInt32() : 0;
            output = usage.TryGetProperty("candidatesTokenCount", out var candidateTokens) ? candidateTokens.GetInt32() : 0;
        }

        return new AiChatResult(text.Length > 0 ? text.ToString() : null, calls, input, output, content.Clone());
    }

    private static JsonNode? ParseOrText(string content)
    {
        try
        {
            return JsonNode.Parse(content);
        }
        catch (JsonException)
        {
            return JsonValue.Create(content);
        }
    }
}
