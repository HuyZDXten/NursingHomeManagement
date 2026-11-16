using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>
/// Quyền xem theo từng cư dân (giữ nguyên tinh thần bản MVC, mở rộng cho 7 role):
/// - Admin: mọi phần.
/// - Receptionist, Accountant: thông tin cơ bản + hóa đơn.
/// - Doctor, Nurse: cơ bản + sức khỏe + chăm sóc + thuốc của mọi cư dân.
/// - Caregiver: như Nurse nhưng CHỈ cư dân đang có việc Pending/InProgress giao cho mình
///   HOẶC mình đang là người phụ trách chính (ResidentCareAssignments, S3-V2).
/// - Family: chỉ cư dân được liên kết, từng phần theo 4 cờ quyền.
/// </summary>
public class ResidentAccessService(
    IResidentRepository residents,
    ICareRepository care,
    IStaffingRepository staffing,
    FacilityClock clock) : IResidentAccessService
{
    public async Task<bool> CanReadAsync(CurrentUser user, int residentId, ResidentSection section)
    {
        if (await residents.GetStatusAsync(residentId) is null)
        {
            throw new NotFoundException("Không tìm thấy cư dân.");
        }

        return user.Role switch
        {
            Roles.Admin => true,
            Roles.Receptionist or Roles.Accountant => section is ResidentSection.Basic or ResidentSection.Billing,
            Roles.Doctor or Roles.Nurse => section != ResidentSection.Billing,
            Roles.Caregiver => section != ResidentSection.Billing && await CaregiverReachesAsync(user.AccountId, residentId),
            Roles.Family => await FamilyCanReadAsync(user.AccountId, residentId, section),
            _ => false
        };
    }

    public async Task RequireAsync(CurrentUser user, int residentId, ResidentSection section)
    {
        if (!await CanReadAsync(user, residentId, section))
        {
            throw new ForbiddenException("Bạn không có quyền xem phần thông tin này của cư dân.");
        }
    }

    public async Task<IReadOnlyCollection<int>?> VisibleResidentIdsAsync(CurrentUser user) => user.Role switch
    {
        Roles.Caregiver => (await care.OpenTaskResidentIdsAsync(user.AccountId))
            .Union(await staffing.PrimaryResidentIdsAsync(user.AccountId, clock.Today))
            .ToList(),
        Roles.Family => (await residents.ListLinksForFamilyAsync(user.AccountId)).Select(link => link.ResidentId).ToList(),
        _ => null
    };

    /// <summary>Có việc đang mở với cư dân, hoặc đang là người phụ trách chính của cư dân.</summary>
    private async Task<bool> CaregiverReachesAsync(int accountId, int residentId) =>
        await care.HasOpenTaskAsync(residentId, accountId)
        || await staffing.IsPrimaryCaregiverAsync(residentId, accountId, clock.Today);

    private async Task<bool> FamilyCanReadAsync(int accountId, int residentId, ResidentSection section)
    {
        var link = await residents.GetLinkAsync(residentId, accountId);
        return link is not null && section switch
        {
            ResidentSection.Basic => true,
            ResidentSection.Health => link.CanViewHealth,
            ResidentSection.Care => link.CanViewCare,
            ResidentSection.Medication => link.CanViewMedication,
            ResidentSection.Billing => link.CanViewBilling,
            _ => false
        };
    }
}
