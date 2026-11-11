using System.ComponentModel.DataAnnotations;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed class BedForm
{
    [Range(1, int.MaxValue)]
    [Display(Name = "Phòng")]
    public int RoomId
    {
        get; set;
    }

    [Required, StringLength(30)]
    [Display(Name = "Mã giường")]
    public string Number { get; set; } = "";

    [Display(Name = "Đang bảo trì")]
    public bool Maintenance
    {
        get; set;
    }
}
