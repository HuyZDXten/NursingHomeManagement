using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed class FamilyDetailsViewModel
{
    public required Resident Resident
    {
        get; init;
    }
    public required ResidentFamilyLink Permissions
    {
        get; init;
    }
    public List<VitalSign> Health { get; init; } = [];
    public List<CareRecord> Care { get; init; } = [];
    public List<MedicationSchedule> Medication { get; init; } = [];
    public List<Invoice> Invoices { get; init; } = [];
    public int Page
    {
        get; init;
    }
}
