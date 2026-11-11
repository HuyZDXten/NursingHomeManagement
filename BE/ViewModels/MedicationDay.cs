using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed record MedicationDay(MedicationSchedule Schedule, DateOnly Date, MedicationLog? Log)
{
    public MedicationStatus Status => Log?.Status ?? MedicationStatus.Scheduled;
}
