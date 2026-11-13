using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Common;
using NursingHome.DTOs.Residents;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>UC-23..UC-28, UC-32: hồ sơ cư dân, người liên hệ, liên kết Family, ngừng lưu trú.</summary>
public class ResidentService(
    IResidentRepository residents,
    IAccountRepository accounts,
    IResidentAccessService access,
    FacilityClock clock) : IResidentService
{
    public async Task<List<ResidentListItemDto>> ListAsync(CurrentUser user)
    {
        var visible = await access.VisibleResidentIdsAsync(user);
        var today = clock.Today;
        return (await residents.ListAsync(visible)).Select(resident => resident.ToListItem(today)).ToList();
    }

    public async Task<ResidentDetailDto> GetAsync(CurrentUser user, int id)
    {
        await access.RequireAsync(user, id, ResidentSection.Basic);
        var resident = await residents.GetDetailAsync(id) ?? throw new NotFoundException("Không tìm thấy cư dân.");
        return resident.ToDetail(clock.Today);
    }

    public async Task<ResidentDetailDto> CreateAsync(SaveResidentRequest request)
    {
        await ValidateAsync(null, request);
        var resident = await residents.AddAsync(new Resident
        {
            Code = request.Code.Trim(),
            FullName = request.FullName.Trim(),
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender.Trim(),
            Notes = request.Notes?.Trim(),
            PhotoUrl = request.PhotoUrl,
            AdmittedAt = clock.UtcNow,
            Status = ResidentStatus.Active,
            CreatedAt = clock.UtcNow
        });
        return resident.ToDetail(clock.Today);
    }

    public async Task<ResidentDetailDto> UpdateAsync(int id, SaveResidentRequest request)
    {
        await ValidateAsync(id, request);
        var resident = await residents.GetByIdAsync(id) ?? throw new NotFoundException("Không tìm thấy cư dân.");
        resident.Code = request.Code.Trim();
        resident.FullName = request.FullName.Trim();
        resident.DateOfBirth = request.DateOfBirth;
        resident.Gender = request.Gender.Trim();
        resident.Notes = request.Notes?.Trim();
        resident.PhotoUrl = request.PhotoUrl;
        resident.UpdatedAt = clock.UtcNow;
        await residents.UpdateAsync(resident);
        return (await residents.GetDetailAsync(id))!.ToDetail(clock.Today);
    }

    public Task DischargeAsync(CurrentUser user, int id, DischargeRequest request) =>
        residents.DischargeAsync(id, user.AccountId, clock.UtcNow, $"Ngừng lưu trú: {request.Reason.Trim()}");

    // ---------------- Người liên hệ ----------------

    public async Task<List<ContactDto>> ListContactsAsync(CurrentUser user, int residentId)
    {
        await access.RequireAsync(user, residentId, ResidentSection.Basic);
        return (await residents.ListContactsAsync(residentId)).Select(contact => contact.ToDto()).ToList();
    }

    public async Task<ContactDto> SaveContactAsync(int residentId, int? contactId, SaveContactRequest request)
    {
        await EnsureResidentAsync(residentId);
        FamilyContact contact;
        if (contactId.HasValue)
        {
            contact = await residents.GetContactAsync(contactId.Value) ?? throw new NotFoundException("Không tìm thấy người liên hệ.");
            if (contact.ResidentId != residentId)
            {
                throw new NotFoundException("Không tìm thấy người liên hệ.");
            }
        }
        else
        {
            contact = new FamilyContact { ResidentId = residentId, CreatedAt = clock.UtcNow };
        }

        contact.FullName = request.FullName.Trim();
        contact.Relationship = request.Relationship.Trim();
        contact.Phone = request.Phone.Trim();
        contact.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        contact.IsEmergencyContact = request.IsEmergencyContact;
        if (contactId.HasValue)
        {
            contact.UpdatedAt = clock.UtcNow;
            await residents.UpdateContactAsync(contact);
        }
        else
        {
            contact = await residents.AddContactAsync(contact);
        }

        return contact.ToDto();
    }

    public async Task DeleteContactAsync(int residentId, int contactId)
    {
        var contact = await residents.GetContactAsync(contactId);
        if (contact is null || contact.ResidentId != residentId)
        {
            throw new NotFoundException("Không tìm thấy người liên hệ.");
        }

        await residents.DeleteContactAsync(contact);
    }

    // ---------------- Liên kết Family ----------------

    public async Task<List<FamilyLinkDto>> ListLinksAsync(int residentId)
    {
        await EnsureResidentAsync(residentId);
        return (await residents.ListLinksAsync(residentId)).Select(link => link.ToDto()).ToList();
    }

    public async Task<FamilyLinkDto> SaveLinkAsync(int residentId, SaveFamilyLinkRequest request)
    {
        await EnsureResidentAsync(residentId);
        if (!await accounts.IsActiveInRolesAsync(request.FamilyAccountId, [Roles.Family]))
        {
            throw new BusinessRuleException("Vui lòng chọn tài khoản Family đang hoạt động.");
        }

        var link = await residents.GetLinkAsync(residentId, request.FamilyAccountId);
        bool isNew = link is null;
        link ??= new ResidentFamilyLink { ResidentId = residentId, FamilyAccountId = request.FamilyAccountId, CreatedAt = clock.UtcNow };
        link.CanViewHealth = request.CanViewHealth;
        link.CanViewCare = request.CanViewCare;
        link.CanViewMedication = request.CanViewMedication;
        link.CanViewBilling = request.CanViewBilling;
        if (isNew)
        {
            await residents.AddLinkAsync(link);
        }
        else
        {
            link.UpdatedAt = clock.UtcNow;
            await residents.UpdateLinkAsync(link);
        }

        return (await residents.ListLinksAsync(residentId)).First(item => item.FamilyAccountId == request.FamilyAccountId).ToDto();
    }

    public async Task DeleteLinkAsync(int residentId, int linkId)
    {
        var link = await residents.GetLinkByIdAsync(linkId);
        if (link is null || link.ResidentId != residentId)
        {
            throw new NotFoundException("Không tìm thấy liên kết.");
        }

        await residents.DeleteLinkAsync(link);
    }

    private async Task ValidateAsync(int? id, SaveResidentRequest request)
    {
        if (request.DateOfBirth > clock.Today || request.DateOfBirth.Year < 1900)
        {
            throw new BusinessRuleException("Ngày sinh không hợp lệ.");
        }

        if (await residents.CodeExistsAsync(request.Code.Trim(), id))
        {
            throw new ConflictException("Mã cư dân đã tồn tại.");
        }
    }

    private async Task EnsureResidentAsync(int residentId)
    {
        if (await residents.GetStatusAsync(residentId) is null)
        {
            throw new NotFoundException("Không tìm thấy cư dân.");
        }
    }
}
