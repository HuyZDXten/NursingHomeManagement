using System.ComponentModel.DataAnnotations;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed class FamilyLinkForm
{
    [Range(1, int.MaxValue)]
    [Display(Name = "Cư dân")]
    public int ResidentId
    {
        get; set;
    }

    [Required]
    [Display(Name = "Tài khoản Family")]
    public string FamilyUserId { get; set; } = "";

    [Display(Name = "Chia sẻ sức khỏe")]
    public bool CanViewHealth
    {
        get; set;
    }

    [Display(Name = "Chia sẻ chăm sóc")]
    public bool CanViewCare
    {
        get; set;
    }

    [Display(Name = "Chia sẻ lịch thuốc")]
    public bool CanViewMedication
    {
        get; set;
    }

    [Display(Name = "Chia sẻ hóa đơn")]
    public bool CanViewBilling
    {
        get; set;
    }
}
