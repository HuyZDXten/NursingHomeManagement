using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed class DashboardViewModel
{
    public int Residents
    {
        get; init;
    }
    public int Rooms
    {
        get; init;
    }
    public int Beds
    {
        get; init;
    }
    public int AvailableBeds
    {
        get; init;
    }
    public int OccupiedBeds
    {
        get; init;
    }
    public int TasksToday
    {
        get; init;
    }
    public int UnfinishedTasks
    {
        get; init;
    }
    public decimal Paid
    {
        get; init;
    }
    public decimal Debt
    {
        get; init;
    }
    public List<ServicePackage> Packages { get; init; } = [];
    public decimal Occupancy => Beds == 0 ? 0 : decimal.Round(OccupiedBeds * 100m / Beds, 1);
}
