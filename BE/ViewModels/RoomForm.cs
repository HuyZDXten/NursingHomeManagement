using System.ComponentModel.DataAnnotations;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed class RoomForm
{
    [Required, StringLength(30)]
    [Display(Name = "Mã phòng")]
    public string Number { get; set; } = "";

    [StringLength(100)]
    [Display(Name = "Mô tả")]
    public string? Description
    {
        get; set;
    }

    [Display(Name = "Đang sử dụng")]
    public bool IsActive { get; set; } = true;
}
