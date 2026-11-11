using System.ComponentModel.DataAnnotations;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed class ContactForm
{
    [Range(1, int.MaxValue)]
    [Display(Name = "Cư dân")]
    public int ResidentId
    {
        get; set;
    }

    [Required, StringLength(150)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = "";

    [Required, StringLength(50)]
    [Display(Name = "Quan hệ")]
    public string Relationship { get; set; } = "";

    [Required, Phone, StringLength(30)]
    [Display(Name = "Điện thoại")]
    public string Phone { get; set; } = "";

    [EmailAddress, StringLength(256)]
    [Display(Name = "Email")]
    public string? Email
    {
        get; set;
    }

    [Display(Name = "Liên hệ khẩn cấp")]
    public bool IsEmergencyContact
    {
        get; set;
    }
}
