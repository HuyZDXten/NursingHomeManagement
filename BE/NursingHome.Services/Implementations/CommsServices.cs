using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DataAccessObjects;
using NursingHome.DTOs.Admin;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.Services.Implementations;

/// <summary>
/// UC-119: Admin gửi thông báo cho nhóm người dùng. Luôn có thông báo trong ứng dụng (chuông + realtime);
/// email chỉ gửi khi Admin tick "gửi email", đã cấu hình SMTP và người nhận chưa tắt kênh "thông báo chung" (UC-120).
/// </summary>
public class AnnouncementService(
    ICommsRepository comms,
    IAccountRepository accounts,
    INotificationService notifications,
    IEmailService email,
    IOptions<FacilityOptions> facility,
    FacilityClock clock,
    ILogger<AnnouncementService> logger) : IAnnouncementService
{
    public async Task<AnnouncementDto> SendAsync(CurrentUser user, CreateAnnouncementRequest request)
    {
        var roles = AnnouncementRules.RolesFor(request.Audience, request.Role);
        string title = request.Title.Trim();
        string body = request.Body.Trim();
        if (title.Length == 0 || body.Length == 0)
        {
            throw new BusinessRuleException("Nhập tiêu đề và nội dung thông báo.");
        }

        var recipients = await accounts.ListActiveByRolesAsync(roles);
        if (recipients.Count == 0)
        {
            throw new BusinessRuleException("Không có tài khoản nào đang hoạt động thuộc đối tượng này.");
        }

        var announcement = await comms.AddAnnouncementAsync(new Announcement
        {
            Title = title,
            Body = body,
            Audience = request.Audience,
            Role = request.Audience == AnnouncementAudiences.Role ? request.Role : null,
            SendEmail = request.SendEmail,
            RecipientCount = recipients.Count,
            CreatedById = user.AccountId,
            CreatedAt = clock.UtcNow
        });

        await notifications.NotifyAsync(recipients.Select(item => item.Id), NotificationType.System, title, AnnouncementRules.NotificationBody(body), null);

        int emailSent = request.SendEmail && email.IsConfigured ? await SendEmailsAsync(recipients, title, body) : 0;
        if (emailSent > 0)
        {
            await comms.SetAnnouncementResultAsync(announcement.Id, recipients.Count, emailSent);
            announcement.EmailSentCount = emailSent;
        }

        announcement.CreatedBy = new Account { Id = user.AccountId, FullName = user.Name };
        return announcement.ToDto();
    }

    public async Task<List<AnnouncementDto>> ListAsync() =>
        (await comms.ListAnnouncementsAsync(100)).Select(item => item.ToDto()).ToList();

    private async Task<int> SendEmailsAsync(List<Account> recipients, string title, string body)
    {
        var addresses = recipients.Select(item => item.Email).ToList();
        var optedOut = await comms.ListOptedOutEmailsAsync(addresses, EmailChannel.Announcements);
        var targets = addresses.Where(address => !optedOut.Contains(address.ToLowerInvariant())).Take(AnnouncementRules.MaxEmailRecipients).ToList();

        string html = $"""
            <p>Kính gửi quý vị,</p>
            <p><b>{WebUtility.HtmlEncode(title)}</b></p>
            <p>{WebUtility.HtmlEncode(body).Replace("\n", "<br>")}</p>
            <p>{WebUtility.HtmlEncode(facility.Value.Name)}{(string.IsNullOrWhiteSpace(facility.Value.Hotline) ? "" : $" · Hotline {WebUtility.HtmlEncode(facility.Value.Hotline)}")}</p>
            <p style="color:#888;font-size:12px">Bạn nhận email này vì đã bật kênh "Thông báo chung". Có thể tắt trong Hồ sơ cá nhân → Kênh nhận thông báo.</p>
            """;
        int sent = 0;
        foreach (var address in targets)
        {
            try
            {
                await email.SendAsync(address, title, html);
                sent++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không gửi được email thông báo tới một người nhận");
            }
        }

        return sent;
    }
}

/// <summary>UC-120: lựa chọn kênh email. Không có dòng = bật hết.</summary>
public class NotificationPreferenceService(ICommsRepository comms, FacilityClock clock) : INotificationPreferenceService
{
    public async Task<NotificationPreferenceDto> GetAsync(CurrentUser user)
    {
        var row = await comms.GetPreferenceAsync(user.AccountId);
        return row is null
            ? new NotificationPreferenceDto()
            : new NotificationPreferenceDto { EmailInvoices = row.EmailInvoices, EmailReminders = row.EmailReminders, EmailAnnouncements = row.EmailAnnouncements };
    }

    public async Task<NotificationPreferenceDto> SaveAsync(CurrentUser user, NotificationPreferenceDto request)
    {
        await comms.UpsertPreferenceAsync(new NotificationPreference
        {
            AccountId = user.AccountId,
            EmailInvoices = request.EmailInvoices,
            EmailReminders = request.EmailReminders,
            EmailAnnouncements = request.EmailAnnouncements,
            UpdatedAt = clock.UtcNow
        });
        return await GetAsync(user);
    }
}
