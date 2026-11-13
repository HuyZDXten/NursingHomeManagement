using Microsoft.Extensions.Options;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Common;
using NursingHome.DTOs.Portal;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.Services.Implementations;

/// <summary>
/// UC-100..UC-102: Family chỉ đọc cư dân được liên kết; mỗi nhóm dữ liệu phụ thuộc cờ quyền,
/// sinh hiệu/chăm sóc còn phải được nhân viên đánh dấu ShareWithFamily ở từng bản ghi.
/// </summary>
public class FamilyService(
    IResidentRepository residents,
    IHealthRepository health,
    ICareRepository care,
    IMedicationRepository medications,
    IBillingRepository billing) : IFamilyService
{
    public async Task<List<FamilyResidentDto>> MyResidentsAsync(CurrentUser user) =>
        (await residents.ListLinksForFamilyAsync(user.AccountId)).Select(link => new FamilyResidentDto
        {
            ResidentId = link.ResidentId,
            Code = link.Resident.Code,
            FullName = link.Resident.FullName,
            DateOfBirth = link.Resident.DateOfBirth,
            Status = link.Resident.Status.ToString(),
            CanViewHealth = link.CanViewHealth,
            CanViewCare = link.CanViewCare,
            CanViewMedication = link.CanViewMedication,
            CanViewBilling = link.CanViewBilling
        }).ToList();

    public async Task<FamilyResidentDetailDto> DetailsAsync(CurrentUser user, int residentId)
    {
        var link = await residents.GetLinkAsync(residentId, user.AccountId)
            ?? throw new NotFoundException("Không tìm thấy cư dân được liên kết.");
        var resident = await residents.GetDetailAsync(residentId) ?? throw new NotFoundException("Không tìm thấy cư dân.");

        return new FamilyResidentDetailDto
        {
            Resident = new FamilyResidentDto
            {
                ResidentId = resident.Id,
                Code = resident.Code,
                FullName = resident.FullName,
                DateOfBirth = resident.DateOfBirth,
                Status = resident.Status.ToString(),
                CanViewHealth = link.CanViewHealth,
                CanViewCare = link.CanViewCare,
                CanViewMedication = link.CanViewMedication,
                CanViewBilling = link.CanViewBilling
            },
            BedLabel = resident.BedLabel(),
            Vitals = link.CanViewHealth
                ? (await health.ListAsync(residentId, null, null, sharedOnly: true, take: 60)).Select(item => item.ToDto()).ToList()
                : [],
            CareRecords = link.CanViewCare
                ? (await care.ListRecordsForResidentAsync(residentId, sharedOnly: true, take: 50)).Select(item => item.ToDto()).ToList()
                : [],
            Medications = link.CanViewMedication
                ? (await medications.ListSchedulesAsync(residentId, null)).Where(item => item.IsActive).Select(item => item.ToDto()).ToList()
                : [],
            Invoices = link.CanViewBilling
                ? (await billing.ListInvoicesAsync(residentId, excludeDraft: true)).Select(item => item.ToListItem()).ToList()
                : []
        };
    }
}

/// <summary>UC-121 dashboard (số liệu truy vấn SQL thật) và UC-14..UC-16 trang công khai.</summary>
public class DashboardService(
    IDashboardRepository dashboard,
    IBillingRepository billing,
    IOptions<FacilityOptions> facility,
    FacilityClock clock) : IDashboardService
{
    public async Task<DashboardDto> GetAsync()
    {
        var start = clock.StartOfDayUtc(clock.Today);
        var stats = await dashboard.GetStatsAsync(start, start.AddDays(1));
        var firstMonth = new DateOnly(clock.Today.Year, clock.Today.Month, 1).AddMonths(-5);
        var monthly = await dashboard.CollectedByMonthAsync(clock.StartOfDayUtc(firstMonth));

        var series = Enumerable.Range(0, 6).Select(offset =>
        {
            var month = firstMonth.AddMonths(offset);
            var row = monthly.FirstOrDefault(item => item.Year == month.Year && item.Month == month.Month);
            return new MonthlyAmountDto { Month = $"{month:MM/yyyy}", Amount = row?.Amount ?? 0 };
        }).ToList();

        return new DashboardDto
        {
            ActiveResidents = stats.ActiveResidents,
            Rooms = stats.Rooms,
            Beds = stats.Beds,
            AvailableBeds = stats.AvailableBeds,
            OccupiedBeds = stats.OccupiedBeds,
            MaintenanceBeds = stats.MaintenanceBeds,
            TasksToday = stats.TasksToday,
            OpenTasks = stats.OpenTasks,
            Billed = stats.Billed,
            Collected = stats.Collected,
            Debt = stats.Billed - stats.Collected,
            UnpaidInvoices = stats.UnpaidInvoices,
            PendingOnlinePayments = stats.PendingOnlinePayments,
            CollectedByMonth = series
        };
    }

    public async Task<PublicOverviewDto> GetPublicOverviewAsync()
    {
        var beds = await dashboard.GetPublicBedStatsAsync();
        var options = facility.Value;
        return new PublicOverviewDto
        {
            FacilityName = options.Name,
            Address = options.Address,
            Hotline = options.Hotline,
            VisitingHours = options.VisitingHours,
            AvailableBeds = beds.AvailableBeds,
            TotalBeds = beds.TotalBeds,
            Packages = (await billing.ListPackagesAsync(activeOnly: true)).Select(package => package.ToDto()).ToList()
        };
    }
}
