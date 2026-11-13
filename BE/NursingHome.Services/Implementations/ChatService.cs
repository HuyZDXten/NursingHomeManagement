using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Chat;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>
/// S3-V6 — UC-106 chat gia đình – nhân viên. Family: chỉ cư dân mình được liên kết. Nhân viên: Admin, Receptionist, Nurse
/// (mọi cư dân), Caregiver (cư dân mình phụ trách chính, S3-V2). Accountant/Doctor không dùng kênh này (403).
/// Người ngoài cuộc trò chuyện đổi id → 404 (không lộ là có tồn tại).
/// </summary>
public class ChatService(
    IChatRepository chat,
    IResidentRepository residents,
    IStaffingRepository staffing,
    IAccountRepository accounts,
    INotificationService notifications,
    IChatPublisher publisher,
    FacilityClock clock) : IChatService
{
    public const int MaxPageSize = 100;
    private const string NotFoundMessage = "Không tìm thấy cuộc trò chuyện.";

    public async Task<List<ConversationDto>> ListAsync(CurrentUser user)
    {
        var summaries = user.IsInRole(Roles.Family)
            ? await chat.ListConversationsAsync(user.AccountId, null, viewerIsFamily: true)
            : await chat.ListConversationsAsync(null, await StaffScopeAsync(user), viewerIsFamily: false);
        return summaries.Select(item => item.ToDto()).ToList();
    }

    public async Task<ConversationDto> OpenAsync(CurrentUser user, int residentId)
    {
        if (await residents.GetLinkAsync(residentId, user.AccountId) is null)
        {
            throw new NotFoundException("Không tìm thấy người thân.");
        }

        var conversation = await chat.GetOrCreateConversationAsync(residentId, user.AccountId, clock.UtcNow);
        return (await chat.ListConversationsAsync(user.AccountId, [residentId], viewerIsFamily: true))
            .First(item => item.Id == conversation.Id).ToDto();
    }

    public async Task<List<ChatMessageDto>> MessagesAsync(CurrentUser user, int conversationId, long? beforeId, int take)
    {
        await RequireAsync(user, conversationId);
        return (await chat.ListMessagesAsync(conversationId, beforeId, Math.Clamp(take, 1, MaxPageSize)))
            .Select(item => item.ToDto()).ToList();
    }

    public async Task<ChatMessageDto> SendAsync(CurrentUser user, int conversationId, SendMessageRequest request)
    {
        var conversation = await RequireAsync(user, conversationId);
        var body = request.Body.Trim();
        if (body.Length is 0 or > 2000)
        {
            throw new BusinessRuleException("Tin nhắn 1–2000 ký tự.");
        }

        var fromFamily = user.IsInRole(Roles.Family);
        var startsStreak = await chat.CountUnreadFromSideAsync(conversationId, fromFamily) == 0;
        var saved = await chat.AddMessageAsync(new ChatMessage
        {
            ConversationId = conversationId,
            SenderId = user.AccountId,
            FromFamily = fromFamily,
            Body = body,
            SentAt = clock.UtcNow
        });
        var dto = saved.ToDto(new Account { FullName = user.Name, Role = user.Role });

        List<int> recipients = fromFamily ? await StaffRecipientsAsync(conversation.ResidentId) : [conversation.FamilyAccountId];
        await publisher.PublishMessageAsync(recipients.Append(user.AccountId).Distinct().ToList(), dto);

        // Chuông chỉ cho tin mở đầu một lượt chưa đọc — không báo dồn dập từng tin.
        if (startsStreak && recipients.Count > 0)
        {
            var link = fromFamily ? $"/staff/messages?c={conversationId}" : $"/family/messages?c={conversationId}";
            var title = fromFamily
                ? $"Tin nhắn mới từ gia đình {conversation.Resident.Code}"
                : $"Tin nhắn mới từ {user.Name}";
            await notifications.NotifyAsync(recipients, NotificationType.System, title, Preview(body), link);
        }

        return dto;
    }

    public async Task<int> MarkReadAsync(CurrentUser user, int conversationId)
    {
        await RequireAsync(user, conversationId);
        return await chat.MarkReadAsync(conversationId, user.IsInRole(Roles.Family), clock.UtcNow);
    }

    // ---------------- Quy tắc ----------------

    /// <summary>Đoạn xem trước trong chuông thông báo (tối đa 100 ký tự).</summary>
    public static string Preview(string body) => body.Length <= 100 ? body : $"{body[..97]}...";

    /// <summary>NULL = mọi cư dân; Caregiver = cư dân đang phụ trách chính; vai trò khác không dùng chat.</summary>
    private async Task<IReadOnlyCollection<int>?> StaffScopeAsync(CurrentUser user) => user.Role switch
    {
        Roles.Admin or Roles.Receptionist or Roles.Nurse => null,
        Roles.Caregiver => await staffing.PrimaryResidentIdsAsync(user.AccountId, clock.Today),
        _ => throw new ForbiddenException("Vai trò của bạn không dùng kênh tin nhắn với gia đình.")
    };

    private async Task<Conversation> RequireAsync(CurrentUser user, int conversationId)
    {
        var conversation = await chat.GetConversationAsync(conversationId);
        if (user.IsInRole(Roles.Family))
        {
            return conversation is not null && conversation.FamilyAccountId == user.AccountId
                && await residents.GetLinkAsync(conversation.ResidentId, user.AccountId) is not null
                ? conversation
                : throw new NotFoundException(NotFoundMessage);
        }

        var scope = await StaffScopeAsync(user);
        return conversation is not null && (scope is null || scope.Contains(conversation.ResidentId))
            ? conversation
            : throw new NotFoundException(NotFoundMessage);
    }

    /// <summary>Gia đình gửi → người phụ trách chính + điều dưỡng có ca hôm nay; không có ai thì mọi lễ tân.</summary>
    private async Task<List<int>> StaffRecipientsAsync(int residentId)
    {
        var today = clock.Today;
        var primaries = (await staffing.ListCareAssignmentsAsync(residentId, null, activeOnly: true))
            .Where(item => item.StartDate <= today)
            .Select(item => item.AccountId);
        var nursesOnShift = (await staffing.ListAssignmentsAsync(today, today, null))
            .Where(item => item.Account.Role == Roles.Nurse && item.Account.IsActive)
            .Select(item => item.AccountId);
        var recipients = primaries.Concat(nursesOnShift).Distinct().ToList();
        return recipients.Count > 0
            ? recipients
            : (await accounts.ListActiveByRolesAsync([Roles.Receptionist])).Select(account => account.Id).ToList();
    }
}
