using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NursingHome.API.Extensions;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Portal;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Hubs;

/// <summary>
/// Kênh realtime /hubs/notifications. Client chỉ nghe sự kiện "notification"; DB vẫn là nguồn sự thật
/// (mất kết nối thì GET /api/notifications lấy lại).
/// </summary>
[Authorize]
public class NotificationHub(IChatService chat) : Hub
{
    public const string Path = "/hubs/notifications";
    public const string EventName = "notification";

    /// <summary>
    /// Client gọi <c>connection.invoke("Typing", conversationId)</c> khi đang soạn tin; bên kia nhận sự kiện "chat-typing".
    /// Cùng quy tắc quyền như gửi tin. Người ngoài cuộc trò chuyện bị bỏ qua im lặng (không lộ cuộc trò chuyện có tồn tại).
    /// </summary>
    public async Task Typing(int conversationId)
    {
        var user = Context.User?.ToCurrentUser();
        if (user is null)
        {
            return;
        }

        try
        {
            await chat.TypingAsync(user, conversationId);
        }
        catch (Exception ex) when (ex is NotFoundException or ForbiddenException)
        {
        }
    }
}

public sealed class SignalRNotificationPublisher(IHubContext<NotificationHub> hub) : INotificationPublisher
{
    public Task PublishAsync(IReadOnlyCollection<int> accountIds, NotificationDto notification) =>
        hub.Clients.Users(accountIds.Select(id => id.ToString())).SendAsync(NotificationHub.EventName, notification);
}
