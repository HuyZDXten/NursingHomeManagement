using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.DTOs.Common;
using NursingHome.DTOs.Portal;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.Services.Implementations;

/// <summary>
/// UC-117: lưu thông báo vào DB (nguồn sự thật) rồi đẩy realtime qua SignalR.
/// Lỗi đẩy realtime không làm hỏng nghiệp vụ chính.
/// </summary>
public class NotificationService(
    INotificationRepository notifications,
    IAccountRepository accounts,
    INotificationPublisher publisher,
    FacilityClock clock,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task NotifyAsync(IEnumerable<int> accountIds, NotificationType type, string title, string? body, string? link)
    {
        var ids = accountIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var now = clock.UtcNow;
        var rows = ids.Select(id => new Notification
        {
            AccountId = id,
            Type = type,
            Title = title,
            Body = body,
            Link = link,
            CreatedAt = now
        }).ToList();
        await notifications.AddRangeAsync(rows);

        try
        {
            await publisher.PublishAsync(ids, rows[0].ToDto());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không đẩy được thông báo realtime");
        }
    }

    public async Task NotifyRolesAsync(IReadOnlyCollection<string> roles, NotificationType type, string title, string? body, string? link)
    {
        var targets = await accounts.ListActiveByRolesAsync(roles);
        await NotifyAsync(targets.Select(account => account.Id), type, title, body, link);
    }

    public async Task<NotificationSummaryDto> ListAsync(CurrentUser user) => new()
    {
        UnreadCount = await notifications.UnreadCountAsync(user.AccountId),
        Items = (await notifications.ListAsync(user.AccountId, 30)).Select(item => item.ToDto()).ToList()
    };

    public Task MarkReadAsync(CurrentUser user, long id) => notifications.MarkReadAsync(id, user.AccountId);

    public Task MarkAllReadAsync(CurrentUser user) => notifications.MarkAllReadAsync(user.AccountId);
}

/// <summary>SMTP qua MailKit (Gmail App Password như ASS2).</summary>
public class EmailService(IOptions<EmailOptions> options) : IEmailService
{
    private readonly EmailOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.SmtpUser));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        var security = Enum.TryParse<SecureSocketOptions>(_options.SmtpSecurity, true, out var parsed) ? parsed : SecureSocketOptions.StartTls;
        using var client = new SmtpClient();
        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, security);
        await client.AuthenticateAsync(_options.SmtpUser, _options.SmtpPassword);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
