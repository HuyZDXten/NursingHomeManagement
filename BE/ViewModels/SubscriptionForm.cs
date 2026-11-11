using System.ComponentModel.DataAnnotations;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed class SubscriptionForm
{
    [Range(1, int.MaxValue)]
    [Display(Name = "Cư dân")]
    public int ResidentId
    {
        get; set;
    }

    [Range(1, int.MaxValue)]
    [Display(Name = "Gói dịch vụ")]
    public int ServicePackageId
    {
        get; set;
    }

    [DataType(DataType.Date)]
    [Display(Name = "Ngày bắt đầu")]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    [DataType(DataType.Date)]
    [Display(Name = "Ngày kết thúc")]
    public DateOnly? EndDate
    {
        get; set;
    }
}
