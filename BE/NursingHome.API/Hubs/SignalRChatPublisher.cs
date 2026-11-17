using Microsoft.AspNetCore.SignalR;
using NursingHome.DTOs.Chat;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Hubs;

/// <summary>
/// S3-V6: tin nhắn realtime dùng lại hub /hubs/notifications, sự kiện "chat". SignalR gửi theo user id = claim sub
/// (SubjectUserIdProvider). DB vẫn là nguồn sự thật: mất kết nối thì GET /api/conversations/{id}/messages lấy lại.
/// </summary>
public sealed class SignalRChatPublisher(IHubContext<NotificationHub> hub) : IChatPublisher
{
    public const string EventName = "chat";
    public const string ReadEventName = "chat-read";
    public const string TypingEventName = "chat-typing";

    public Task PublishMessageAsync(IReadOnlyCollection<int> accountIds, ChatMessageDto message) =>
        hub.Clients.Users(accountIds.Select(id => id.ToString())).SendAsync(EventName, message);

    public Task PublishReadAsync(IReadOnlyCollection<int> accountIds, ChatReadDto read) =>
        hub.Clients.Users(accountIds.Select(id => id.ToString())).SendAsync(ReadEventName, read);

    public Task PublishTypingAsync(IReadOnlyCollection<int> accountIds, ChatTypingDto typing) =>
        hub.Clients.Users(accountIds.Select(id => id.ToString())).SendAsync(TypingEventName, typing);
}
