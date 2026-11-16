using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Options;

namespace NursingHome.Services.Ai;

/// <summary>
/// Vòng lặp hỏi–đáp dùng chung cho mọi kênh AI (S4-V1): nạp lịch sử, gọi provider, chạy tool tối đa
/// <see cref="MaxToolRounds"/> vòng (sau đó buộc model trả lời không dùng tool), lưu tin + token.
/// Lỗi/timeout provider → câu xin lỗi kèm hotline, không ném 500. Không ghi log nội dung hội thoại.
/// </summary>
public sealed class AiChatEngine(
    IAiRepository ai,
    IAiChatProvider provider,
    IOptions<FacilityOptions> facility,
    FacilityClock clock,
    ILogger<AiChatEngine> logger) : IAiChatEngine
{
    public const int HistoryTake = 20;
    public const int MaxToolRounds = 3;
    public const int MaxMessageLength = 1000;
    private const int MaxToolResultLength = 8000;

    public async Task<AiReply> ChatAsync(string channel, CurrentUser? user, string? anonymousKey, string message,
        string systemPrompt, IReadOnlyList<IAiTool> tools, CancellationToken cancellationToken = default)
    {
        var question = message.Trim();
        if (question.Length is 0 or > MaxMessageLength)
        {
            throw new BusinessRuleException($"Tin nhắn 1–{MaxMessageLength} ký tự.");
        }

        var conversation = await ai.GetOrCreateConversationAsync(channel, user?.AccountId, anonymousKey, clock.UtcNow);
        var messages = (await ai.ListMessagesAsync(conversation.Id, HistoryTake, includeTools: false))
            .Select(item => new AiMessage(item.Role, item.Content))
            .Append(new AiMessage(AiRoles.User, question))
            .ToList();
        var records = new List<AiMessageRecord> { new() { Role = AiRoles.User, Content = question, CreatedAt = clock.UtcNow } };
        var definitions = tools.Select(tool => tool.Definition).ToList();
        var byName = tools.ToDictionary(tool => tool.Definition.Name, StringComparer.Ordinal);
        int inputTokens = 0, outputTokens = 0;
        string? answer;

        try
        {
            for (var round = 0; ; round++)
            {
                IReadOnlyList<AiToolDefinition> offered = round < MaxToolRounds ? definitions : [];
                var result = await provider.CompleteAsync(new AiChatRequest(systemPrompt, messages, offered), cancellationToken);
                inputTokens += result.InputTokens;
                outputTokens += result.OutputTokens;
                if (result.ToolCalls.Count == 0 || offered.Count == 0)
                {
                    answer = result.Text;
                    break;
                }

                messages.Add(new AiMessage(AiRoles.Assistant, result.Text ?? "", Native: result.Native));
                foreach (var call in result.ToolCalls)
                {
                    var output = await ExecuteToolAsync(byName, call, user, cancellationToken);
                    messages.Add(new AiMessage(AiRoles.Tool, output, call.Name, call.Id));
                    records.Add(new AiMessageRecord { Role = AiRoles.Tool, Content = output, ToolName = call.Name, CreatedAt = clock.UtcNow });
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is AiProviderException or HttpRequestException or OperationCanceledException or JsonException)
        {
            logger.LogWarning("Provider AI {Provider} lỗi ({Error}) ở kênh {Channel}", provider.Name, ex.GetType().Name, channel);
            answer = BusyMessage();
        }

        if (string.IsNullOrWhiteSpace(answer))
        {
            answer = BusyMessage();
        }

        records.Add(new AiMessageRecord
        {
            Role = AiRoles.Assistant,
            Content = answer.Trim(),
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CreatedAt = clock.UtcNow
        });
        await ai.AddMessagesAsync(conversation.Id, records, Title(question), clock.UtcNow);
        return new AiReply(answer.Trim(), conversation.Id);
    }

    public async Task<List<AiMessage>> HistoryAsync(string channel, int? accountId, string? anonymousKey, int take = 50)
    {
        var conversation = await ai.FindConversationAsync(channel, accountId, anonymousKey);
        return conversation is null
            ? []
            : (await ai.ListMessagesAsync(conversation.Id, Math.Clamp(take, 1, 200), includeTools: false))
                .Select(item => new AiMessage(item.Role, item.Content)).ToList();
    }

    /// <summary>Lỗi nghiệp vụ/quyền của tool trả về model dạng {"error": "..."} để model giải thích; lỗi khác chỉ ghi log.</summary>
    private async Task<string> ExecuteToolAsync(IReadOnlyDictionary<string, IAiTool> tools, AiToolCall call, CurrentUser? user, CancellationToken cancellationToken)
    {
        if (!tools.TryGetValue(call.Name, out var tool))
        {
            return AiToolJson.Serialize(new { error = $"Không có công cụ {call.Name}." });
        }

        try
        {
            var output = await tool.ExecuteAsync(user, call.Arguments, cancellationToken);
            return output.Length <= MaxToolResultLength ? output : output[..MaxToolResultLength];
        }
        catch (Exception ex) when (ex is BusinessRuleException or NotFoundException or ForbiddenException or ConflictException)
        {
            return AiToolJson.Serialize(new { error = ex.Message });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Tool AI {Tool} lỗi", call.Name);
            return AiToolJson.Serialize(new { error = "Lỗi hệ thống khi tra cứu." });
        }
    }

    private string BusyMessage() =>
        $"Xin lỗi, trợ lý đang bận. Vui lòng thử lại sau ít phút{(string.IsNullOrWhiteSpace(facility.Value.Hotline) ? "" : $" hoặc gọi hotline {facility.Value.Hotline}")}.";

    private static string Title(string question) => question.Length <= 60 ? question : $"{question[..57]}...";
}
