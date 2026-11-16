using Microsoft.Extensions.Options;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Ai;
using NursingHome.DTOs.Common;
using NursingHome.Services.Ai.Tools;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.Services.Ai;

/// <summary>S4-H1 — AI-02: trợ lý AI dành cho gia đình, chỉ đọc dữ liệu qua service có sẵn theo quyền.</summary>
public interface IFamilyAssistantService
{
    Task<AiChatReplyDto> ChatAsync(CurrentUser user, string message, CancellationToken cancellationToken = default);

    Task<List<AiHistoryItemDto>> HistoryAsync(CurrentUser user);
}

public sealed class FamilyAssistantService(
    IAiChatEngine engine,
    IFamilyService family,
    IFamilyTimelineService timeline,
    IHealthService health,
    IVisitService visits,
    IOptions<AiOptions> options,
    IOptions<FacilityOptions> facility,
    FacilityClock clock) : IFamilyAssistantService
{
    public async Task<AiChatReplyDto> ChatAsync(CurrentUser user, string message, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var tools = FamilyTools.Create(family, timeline, health, visits, clock);
        var reply = await engine.ChatAsync(AiChannels.Family, user, null, message, SystemPrompt(user), tools, cancellationToken);
        return new AiChatReplyDto { Reply = reply.Text };
    }

    public async Task<List<AiHistoryItemDto>> HistoryAsync(CurrentUser user)
    {
        EnsureEnabled();
        return (await engine.HistoryAsync(AiChannels.Family, user.AccountId, null))
            .Select(item => new AiHistoryItemDto { Role = item.Role, Content = item.Content })
            .ToList();
    }

    private string SystemPrompt(CurrentUser user) => $"""
        Bạn là trợ lý thân thiện của {facility.Value.Name}, hỗ trợ người nhà ({user.Name}) nắm tình hình người thân bằng tiếng Việt ấm áp, tối đa 6 câu.
        Hôm nay là {clock.Today:yyyy-MM-dd}. Luôn gọi công cụ (list_my_residents trước nếu chưa rõ residentId) để tra cứu dữ liệu thật; nếu gia đình có nhiều người thân và câu hỏi chưa rõ hỏi ai thì hỏi lại.
        Chỉ nói thông tin có trong kết quả công cụ; nếu chưa có dữ liệu hoặc không có quyền xem thì nói rõ, tuyệt đối không bịa hoặc trả lời về cư dân khác.
        Không chẩn đoán bệnh, không đánh giá chỉ số là nguy hiểm hay bình thường, không khuyên dùng hay ngừng thuốc. Khi người nhà lo lắng, khuyên nhắn tin cho điều dưỡng qua mục Tin nhắn hoặc gọi hotline {facility.Value.Hotline ?? "của cơ sở"}.
        """;

    private void EnsureEnabled()
    {
        if (!options.Value.Enabled)
        {
            throw new ServiceUnavailableException("Trợ lý AI đang tắt (Ai:Enabled = false).");
        }
    }
}
