using System.Text.RegularExpressions;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Common;
using NursingHome.DTOs.Staffing;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>
/// BL-V4, UC-43: hồ sơ nhân viên và chứng chỉ. CCCD là dữ liệu nhạy cảm: chỉ Admin và chính nhân viên đó xem.
/// Chứng chỉ sắp hết hạn: mỗi mốc (còn ≤ 30 ngày, đã hết hạn) báo MỘT lần cho nhân viên và Admin.
/// </summary>
public partial class StaffHrService(
    IStaffHrRepository hr,
    IAccountRepository accounts,
    INotificationService notifications,
    FacilityClock clock) : IStaffHrService
{
    private const string HrLink = "/staff/hr";

    public async Task<List<StaffProfileSummaryDto>> ListProfilesAsync()
    {
        var staff = await hr.ListStaffAccountsAsync(Roles.Staff);
        var profiles = (await hr.ListProfilesAsync()).ToDictionary(item => item.AccountId);
        var certificates = (await hr.ListCertificatesAsync(null)).ToLookup(item => item.AccountId);
        var today = clock.Today;
        return staff.Select(account =>
        {
            profiles.TryGetValue(account.Id, out var profile);
            var states = certificates[account.Id].Select(item => StaffHrRules.StateOf(item.ExpiresOn, today)).ToList();
            return new StaffProfileSummaryDto
            {
                AccountId = account.Id,
                FullName = account.FullName,
                Email = account.Email,
                Role = account.Role,
                IsActive = account.IsActive,
                Position = profile?.Position,
                Department = profile?.Department,
                HireDate = profile?.HireDate,
                CertificateCount = states.Count,
                ExpiringCount = states.Count(state => state == CertificateState.ExpiringSoon),
                ExpiredCount = states.Count(state => state == CertificateState.Expired)
            };
        }).ToList();
    }

    public async Task<StaffProfileDto> GetProfileAsync(CurrentUser user, int accountId)
    {
        if (!user.IsInRole(Roles.Admin) && user.AccountId != accountId)
        {
            throw new ForbiddenException("Bạn chỉ xem được hồ sơ của chính mình.");
        }

        return await BuildProfileAsync(await RequireStaffAsync(accountId));
    }

    public async Task<StaffProfileDto> SaveProfileAsync(int accountId, SaveStaffProfileRequest request)
    {
        var account = await RequireStaffAsync(accountId);
        string? idNumber = Clean(request.IdNumber);
        if (idNumber is not null && !IdNumberPattern().IsMatch(idNumber))
        {
            throw new BusinessRuleException("Số CCCD gồm 12 chữ số (CMND cũ 9 chữ số).");
        }

        if (request.HireDate is { } hired && hired > clock.Today)
        {
            throw new BusinessRuleException("Ngày vào làm không được ở tương lai.");
        }

        await hr.UpsertProfileAsync(new StaffProfile
        {
            AccountId = accountId,
            Position = Clean(request.Position),
            Department = Clean(request.Department),
            HireDate = request.HireDate,
            IdNumber = idNumber,
            Address = Clean(request.Address),
            EmergencyContactName = Clean(request.EmergencyContactName),
            EmergencyContactPhone = Clean(request.EmergencyContactPhone),
            Notes = Clean(request.Notes),
            UpdatedAt = clock.UtcNow
        });
        return await BuildProfileAsync(account);
    }

    public async Task<StaffCertificateDto> AddCertificateAsync(CurrentUser user, int accountId, SaveCertificateRequest request)
    {
        var account = await RequireStaffAsync(accountId);
        StaffHrRules.ValidateCertificate(request.Name, request.IssuedOn, request.ExpiresOn, clock.Today);
        var created = await hr.AddCertificateAsync(new StaffCertificate
        {
            AccountId = accountId,
            Name = request.Name.Trim(),
            Number = Clean(request.Number),
            IssuedBy = Clean(request.IssuedBy),
            IssuedOn = request.IssuedOn,
            ExpiresOn = request.ExpiresOn,
            CreatedById = user.AccountId,
            CreatedAt = clock.UtcNow
        });
        created.Account = account;
        return created.ToDto(clock.Today);
    }

    public async Task<StaffCertificateDto> UpdateCertificateAsync(int id, SaveCertificateRequest request)
    {
        StaffHrRules.ValidateCertificate(request.Name, request.IssuedOn, request.ExpiresOn, clock.Today);
        bool updated = await hr.UpdateCertificateAsync(id, certificate =>
        {
            certificate.Name = request.Name.Trim();
            certificate.Number = Clean(request.Number);
            certificate.IssuedBy = Clean(request.IssuedBy);
            certificate.IssuedOn = request.IssuedOn;
            certificate.ExpiresOn = request.ExpiresOn;
        });
        if (!updated)
        {
            throw new NotFoundException("Không tìm thấy chứng chỉ.");
        }

        return (await hr.GetCertificateAsync(id))!.ToDto(clock.Today);
    }

    public async Task DeleteCertificateAsync(int id)
    {
        if (!await hr.DeleteCertificateAsync(id))
        {
            throw new NotFoundException("Không tìm thấy chứng chỉ.");
        }
    }

    public async Task<List<StaffCertificateDto>> ListExpiringAsync()
    {
        var today = clock.Today;
        return (await hr.ListExpiringAsync(today.AddDays(StaffHrRules.CertificateAlertDays))).Select(item => item.ToDto(today)).ToList();
    }

    public async Task<CertificateAlertResultDto> SendExpiryAlertsAsync(CancellationToken cancellationToken = default)
    {
        var today = clock.Today;
        var candidates = await hr.ListExpiringAsync(today.AddDays(StaffHrRules.CertificateAlertDays));
        var adminIds = (await accounts.ListActiveByRolesAsync([Roles.Admin])).Select(item => item.Id).ToList();
        int sent = 0;
        foreach (var certificate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (StaffHrRules.PendingAlert(certificate, today) is not { } stage)
            {
                continue;
            }

            // Đánh dấu có điều kiện TRƯỚC khi gửi: hai instance cùng chạy thì chỉ một bên báo.
            bool expired = stage == CertificateState.Expired;
            if (!await hr.MarkAlertedAsync(certificate.Id, expired, clock.UtcNow))
            {
                continue;
            }

            string staffName = certificate.Account.FullName;
            string when = certificate.ExpiresOn!.Value.ToString("dd/MM/yyyy");
            string title = expired ? $"Chứng chỉ đã hết hạn: {certificate.Name}" : $"Chứng chỉ sắp hết hạn: {certificate.Name}";
            string body = expired
                ? $"{staffName} — hết hạn ngày {when}. Cần gia hạn hoặc cập nhật chứng chỉ mới."
                : $"{staffName} — hết hạn ngày {when} (còn {StaffHrRules.DaysLeft(certificate.ExpiresOn, today)} ngày).";
            await notifications.NotifyAsync(adminIds.Append(certificate.AccountId).Distinct().ToList(), NotificationType.System, title, body, HrLink);
            sent++;
        }

        return new CertificateAlertResultDto { Checked = candidates.Count, Sent = sent };
    }

    // ---------------- Hỗ trợ ----------------

    private async Task<Account> RequireStaffAsync(int accountId)
    {
        var account = await accounts.GetByIdAsync(accountId);
        return account is { } found && found.Role != Roles.Family ? found : throw new NotFoundException("Không tìm thấy nhân viên.");
    }

    private async Task<StaffProfileDto> BuildProfileAsync(Account account)
    {
        var profile = await hr.GetProfileAsync(account.Id);
        var today = clock.Today;
        var certificates = await hr.ListCertificatesAsync(account.Id);
        return new StaffProfileDto
        {
            AccountId = account.Id,
            FullName = account.FullName,
            Email = account.Email,
            Phone = account.Phone,
            Role = account.Role,
            IsActive = account.IsActive,
            Position = profile?.Position,
            Department = profile?.Department,
            HireDate = profile?.HireDate,
            IdNumber = profile?.IdNumber,
            Address = profile?.Address,
            EmergencyContactName = profile?.EmergencyContactName,
            EmergencyContactPhone = profile?.EmergencyContactPhone,
            Notes = profile?.Notes,
            UpdatedAt = profile?.UpdatedAt,
            Certificates = certificates.Select(item => item.ToDto(today)).ToList()
        };
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"^(\d{9}|\d{12})$")]
    private static partial Regex IdNumberPattern();
}
