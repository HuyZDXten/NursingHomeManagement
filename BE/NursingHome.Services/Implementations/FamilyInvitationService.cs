using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Accounts;
using NursingHome.DTOs.Auth;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.Services.Implementations;

/// <summary>
/// BL-V1, UC-08: lễ tân tạo mã mời cho một người thân của cư dân; người thân mở link /kich-hoat?code=...,
/// đặt họ tên + mật khẩu → có tài khoản Family đã liên kết sẵn với cư dân theo quyền lễ tân chọn.
/// Có SMTP thì gửi link qua email; không có thì lễ tân chép mã/link (chỉ hiện một lần) gửi cho người thân.
/// </summary>
public class FamilyInvitationService(
    IFamilyInvitationRepository invitations,
    IResidentRepository residents,
    IAccountRepository accounts,
    IAuthService auth,
    IEmailService email,
    IOptions<EmailOptions> emailOptions,
    IOptions<FacilityOptions> facility,
    FacilityClock clock,
    ILogger<FamilyInvitationService> logger) : IFamilyInvitationService
{
    private const int ListTake = 200;

    public async Task<FamilyInvitationCreatedDto> CreateAsync(CurrentUser user, CreateFamilyInvitationRequest request)
    {
        var resident = await residents.GetByIdAsync(request.ResidentId) ?? throw new NotFoundException("Không tìm thấy cư dân.");
        if (resident.Status != ResidentStatus.Active)
        {
            throw new BusinessRuleException("Cư dân đã ngừng lưu trú, không mời thêm người thân.");
        }

        string address = request.Email.Trim().ToLowerInvariant();
        if (await accounts.GetByEmailAsync(address) is not null)
        {
            throw new ConflictException("Email này đã có tài khoản. Dùng nút \"Liên kết tài khoản gia đình\" để liên kết.");
        }

        var now = clock.UtcNow;
        string code = InvitationRules.NewCode();
        var created = await invitations.CreateAsync(new FamilyInvitation
        {
            CodeHash = InvitationRules.Hash(code),
            Email = address,
            ResidentId = resident.Id,
            Relationship = request.Relationship.Trim(),
            CanViewHealth = request.CanViewHealth,
            CanViewCare = request.CanViewCare,
            CanViewMedication = request.CanViewMedication,
            CanViewBilling = request.CanViewBilling,
            CreatedById = user.AccountId,
            CreatedAt = now,
            ExpiresAt = now.AddDays(InvitationRules.ExpiryDays)
        }, now);

        string link = InvitationRules.Link(emailOptions.Value.FrontendBaseUrl, code);
        bool sent = await TrySendAsync(address, resident.FullName, link, created.ExpiresAt);
        var stored = await invitations.GetAsync(created.Id) ?? created;
        return new FamilyInvitationCreatedDto { Invitation = stored.ToDto(now), Code = code, Link = link, EmailSent = sent };
    }

    public async Task<List<FamilyInvitationDto>> ListAsync(int? residentId)
    {
        var now = clock.UtcNow;
        return (await invitations.ListAsync(residentId, ListTake)).Select(item => item.ToDto(now)).ToList();
    }

    public async Task<FamilyInvitationDto> RevokeAsync(int id)
    {
        _ = await invitations.GetAsync(id) ?? throw new NotFoundException("Không tìm thấy mã mời.");
        if (!await invitations.RevokeAsync(id, clock.UtcNow))
        {
            throw new BusinessRuleException("Mã mời đã được dùng hoặc đã thu hồi.");
        }

        return (await invitations.GetAsync(id))!.ToDto(clock.UtcNow);
    }

    public async Task<InvitationLookupDto> LookupAsync(string code)
    {
        var invitation = await FindUsableAsync(code);
        return new InvitationLookupDto
        {
            Email = invitation.Email,
            ResidentName = invitation.Resident.FullName,
            Relationship = invitation.Relationship,
            ExpiresAt = invitation.ExpiresAt,
            FacilityName = facility.Value.Name
        };
    }

    public async Task<LoginResponse> AcceptAsync(AcceptInvitationRequest request)
    {
        var invitation = await FindUsableAsync(request.Code);
        var now = clock.UtcNow;
        await invitations.AcceptAsync(invitation.Id, new Account
        {
            Email = invitation.Email,
            PasswordHash = PasswordHasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Role = Roles.Family,
            IsActive = true,
            CreatedAt = now
        }, now);

        // Đăng nhập luôn bằng mật khẩu vừa đặt (cùng luồng cấp token với đăng nhập thường).
        return await auth.LoginAsync(new LoginRequest { Email = invitation.Email, Password = request.Password });
    }

    private async Task<FamilyInvitation> FindUsableAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new NotFoundException("Mã mời không đúng.");
        }

        var invitation = await invitations.GetByHashAsync(InvitationRules.Hash(code)) ?? throw new NotFoundException("Mã mời không đúng.");
        string? reason = InvitationRules.UnusableReason(InvitationRules.StatusOf(invitation, clock.UtcNow));
        return reason is null ? invitation : throw new BusinessRuleException(reason);
    }

    private async Task<bool> TrySendAsync(string address, string residentName, string link, DateTime expiresAt)
    {
        if (!email.IsConfigured)
        {
            return false;
        }

        string facilityName = WebUtility.HtmlEncode(facility.Value.Name);
        string body = $"""
            <p>Xin chào,</p>
            <p>{facilityName} mời anh/chị tạo tài khoản gia đình để theo dõi thông tin của
            <b>{WebUtility.HtmlEncode(residentName)}</b>.</p>
            <p><a href="{WebUtility.HtmlEncode(link)}">Bấm vào đây để kích hoạt tài khoản</a>
            (hiệu lực đến {expiresAt + FacilityClock.Offset:dd/MM/yyyy HH:mm}).</p>
            <p>Nếu anh/chị không chờ thư này, hãy bỏ qua.</p>
            """;
        try
        {
            await email.SendAsync(address, $"Mời kích hoạt tài khoản gia đình — {facility.Value.Name}", body);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không gửi được email mã mời");
            return false;
        }
    }
}
