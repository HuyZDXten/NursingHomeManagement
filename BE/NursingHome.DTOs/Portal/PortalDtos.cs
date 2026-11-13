using NursingHome.DTOs.Billing;
using NursingHome.DTOs.Care;
using NursingHome.DTOs.Medication;

namespace NursingHome.DTOs.Portal;

// ---------------- Cổng Family (UC-100..UC-102) ----------------

public class FamilyResidentDto
{
    public int ResidentId { get; set; }
    public string Code { get; set; } = "";
    public string FullName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public string Status { get; set; } = "";
    public bool CanViewHealth { get; set; }
    public bool CanViewCare { get; set; }
    public bool CanViewMedication { get; set; }
    public bool CanViewBilling { get; set; }
}

public class FamilyResidentDetailDto
{
    public FamilyResidentDto Resident { get; set; } = new();
    public string? BedLabel { get; set; }
    public List<VitalSignDto> Vitals { get; set; } = [];
    public List<CareRecordDto> CareRecords { get; set; } = [];
    public List<MedicationScheduleDto> Medications { get; set; } = [];
    public List<InvoiceListItemDto> Invoices { get; set; } = [];
}

// ---------------- Dashboard (UC-121) & trang công khai (UC-14..UC-16) ----------------

public class DashboardDto
{
    public int ActiveResidents { get; set; }
    public int Rooms { get; set; }
    public int Beds { get; set; }
    public int AvailableBeds { get; set; }
    public int OccupiedBeds { get; set; }
    public int MaintenanceBeds { get; set; }
    public int TasksToday { get; set; }
    public int OpenTasks { get; set; }
    public decimal Billed { get; set; }
    public decimal Collected { get; set; }
    public decimal Debt { get; set; }
    public int UnpaidInvoices { get; set; }
    public int PendingOnlinePayments { get; set; }
    public List<MonthlyAmountDto> CollectedByMonth { get; set; } = [];
}

public class MonthlyAmountDto
{
    public string Month { get; set; } = "";
    public decimal Amount { get; set; }
}

public class PublicOverviewDto
{
    public string FacilityName { get; set; } = "";
    public string? Address { get; set; }
    public string? Hotline { get; set; }
    public string? VisitingHours { get; set; }
    public int AvailableBeds { get; set; }
    public int TotalBeds { get; set; }
    public List<ServicePackageDto> Packages { get; set; } = [];
}

// ---------------- Thông báo (UC-117) ----------------

public class NotificationDto
{
    public long Id { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Body { get; set; }
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationSummaryDto
{
    public int UnreadCount { get; set; }
    public List<NotificationDto> Items { get; set; } = [];
}
