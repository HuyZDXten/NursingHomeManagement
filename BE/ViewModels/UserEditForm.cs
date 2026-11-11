using System.ComponentModel.DataAnnotations;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed class UserEditForm
{
    [Required, StringLength(150)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = "";

    [Required]
    [Display(Name = "Vai trò")]
    public string Role { get; set; } = Roles.Caregiver;

    [Display(Name = "Cho phép đăng nhập")]
    public bool IsActive { get; set; } = true;
}
