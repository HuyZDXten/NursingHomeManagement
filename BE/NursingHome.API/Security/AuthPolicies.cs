using NursingHome.BusinessObjects.Enums;

namespace NursingHome.API.Security;

/// <summary>
/// Policy theo nhóm chức năng (ma trận phân quyền trong docs/HANDOVER.md).
/// Policy chỉ là lớp ngoài; quyền theo từng cư dân vẫn do IResidentAccessService kiểm tra ở Service.
/// </summary>
public static class AuthPolicies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string Staff = nameof(Staff);
    public const string FrontDesk = nameof(FrontDesk);
    public const string BillingStaff = nameof(BillingStaff);
    public const string FinanceStaff = nameof(FinanceStaff);
    public const string InvoiceReaders = nameof(InvoiceReaders);
    public const string ClinicalStaff = nameof(ClinicalStaff);
    public const string CarePlanners = nameof(CarePlanners);
    public const string Prescribers = nameof(Prescribers);
    public const string FamilyOnly = nameof(FamilyOnly);

    public static readonly IReadOnlyDictionary<string, string[]> Map = new Dictionary<string, string[]>
    {
        [AdminOnly] = [Roles.Admin],
        [Staff] = Roles.Staff,
        [FrontDesk] = [Roles.Admin, Roles.Receptionist],
        [BillingStaff] = [Roles.Admin, Roles.Receptionist, Roles.Accountant],
        [FinanceStaff] = [Roles.Admin, Roles.Accountant],
        [InvoiceReaders] = [Roles.Admin, Roles.Receptionist, Roles.Accountant, Roles.Family],
        [ClinicalStaff] = [Roles.Admin, Roles.Doctor, Roles.Nurse, Roles.Caregiver],
        [CarePlanners] = [Roles.Admin, Roles.Nurse],
        [Prescribers] = [Roles.Admin, Roles.Doctor],
        [FamilyOnly] = [Roles.Family]
    };
}

public static class RateLimitPolicies
{
    public const string Credentials = nameof(Credentials);
}
