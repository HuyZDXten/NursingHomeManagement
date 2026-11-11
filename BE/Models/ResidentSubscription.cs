using System.ComponentModel.DataAnnotations;

namespace NursingHomeManagement.Models;

public class ResidentSubscription : BaseEntity
{
    public int ResidentId
    {
        get; set;
    }

    public Resident Resident { get; set; } = null!;

    public int ServicePackageId
    {
        get; set;
    }

    public ServicePackage ServicePackage { get; set; } = null!;

    public DateOnly StartDate
    {
        get; set;
    }

    public DateOnly? EndDate
    {
        get; set;
    }

    public decimal AgreedMonthlyPrice
    {
        get; set;
    }
}
