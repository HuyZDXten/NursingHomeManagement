using NursingHome.BusinessObjects.Entities;
using NursingHome.DTOs.Accounts;

namespace NursingHome.Services.Helpers;

public static partial class OperationsMapper
{
    public static FamilyInvitationDto ToDto(this FamilyInvitation invitation, DateTime now) => new()
    {
        Id = invitation.Id,
        ResidentId = invitation.ResidentId,
        ResidentCode = invitation.Resident?.Code ?? "",
        ResidentName = invitation.Resident?.FullName ?? "",
        Email = invitation.Email,
        Relationship = invitation.Relationship,
        CanViewHealth = invitation.CanViewHealth,
        CanViewCare = invitation.CanViewCare,
        CanViewMedication = invitation.CanViewMedication,
        CanViewBilling = invitation.CanViewBilling,
        Status = InvitationRules.StatusOf(invitation, now),
        CreatedAt = invitation.CreatedAt,
        ExpiresAt = invitation.ExpiresAt,
        UsedAt = invitation.UsedAt,
        CreatedByName = invitation.CreatedBy?.FullName,
        AccountName = invitation.Account?.FullName
    };
}
