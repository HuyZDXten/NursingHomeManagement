using System.ComponentModel.DataAnnotations;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed class PackageForm
{
    [Required, StringLength(150)]
    [Display(Name = "Tên gói")]
    public string Name { get; set; } = "";

    [Range(typeof(decimal), "0", "999999999999")]
    [Display(Name = "Phí hàng tháng")]
    public decimal MonthlyPrice
    {
        get; set;
    }

    [StringLength(2000)]
    [Display(Name = "Mô tả")]
    public string? Description
    {
        get; set;
    }

    [Display(Name = "Đang cung cấp")]
    public bool IsActive { get; set; } = true;
}
