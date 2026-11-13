using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NursingHome.DTOs.Portal;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Hubs;

/// <summary>
/// Kênh realtime /hubs/notifications. Client chỉ nghe sự kiện "notification"; DB vẫn là nguồn sự thật
/// (mất kết nối thì GET /api/notifications lấy lại).
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    public const string Path = "/hubs/notifications";
    public const string EventName = "notification";
}

public sealed class SignalRNotificationPublisher(IHubContext<NotificationHub> hub) : INotificationPublisher
{
    public Task PublishAsync(IReadOnlyCollection<int> accountIds, NotificationDto notification) =>
        hub.Clients.Users(accountIds.Select(id => id.ToString())).SendAsync(NotificationHub.EventName, notification);
}
