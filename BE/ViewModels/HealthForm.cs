using System.ComponentModel.DataAnnotations;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed class HealthForm
{
    [Range(1, int.MaxValue)]
    [Display(Name = "Cư dân")]
    public int ResidentId
    {
        get; set;
    }

    [Range(40, 300)]
    [Display(Name = "Huyết áp tâm thu")]
    public int Systolic { get; set; } = 120;

    [Range(20, 200)]
    [Display(Name = "Huyết áp tâm trương")]
    public int Diastolic { get; set; } = 80;

    [Range(20, 250)]
    [Display(Name = "Nhịp tim")]
    public int HeartRate { get; set; } = 75;

    [Range(25, 45)]
    [Display(Name = "Nhiệt độ °C")]
    public decimal Temperature { get; set; } = 36.5m;

    [Range(0, 100)]
    [Display(Name = "SpO2 %")]
    public decimal? SpO2
    {
        get; set;
    }

    [StringLength(2000)]
    [Display(Name = "Ghi chú quan sát")]
    public string? Notes
    {
        get; set;
    }

    [Display(Name = "Cho phép chia sẻ với gia đình")]
    public bool ShareWithFamily
    {
        get; set;
    }
}
