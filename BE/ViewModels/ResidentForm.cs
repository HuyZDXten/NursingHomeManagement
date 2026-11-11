using System.ComponentModel.DataAnnotations;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed class ResidentForm
{
    [Required, StringLength(30)]
    [Display(Name = "Mã cư dân")]
    public string Code { get; set; } = "";

    [Required, StringLength(150)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = "";

    [DataType(DataType.Date)]
    [Display(Name = "Ngày sinh")]
    public DateOnly DateOfBirth { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(-70));

    [Required, StringLength(20)]
    [Display(Name = "Giới tính")]
    public string Gender { get; set; } = "";

    [StringLength(2000)]
    [Display(Name = "Ghi chú")]
    public string? Notes
    {
        get; set;
    }
}
