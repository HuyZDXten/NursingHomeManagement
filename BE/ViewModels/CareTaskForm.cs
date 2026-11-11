using System.ComponentModel.DataAnnotations;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed class CareTaskForm
{
    [Range(1, int.MaxValue)]
    [Display(Name = "Cư dân")]
    public int ResidentId
    {
        get; set;
    }

    [Required]
    [Display(Name = "Người chăm sóc")]
    public string AssignedToId { get; set; } = "";

    [Required, StringLength(200)]
    [Display(Name = "Công việc")]
    public string Title { get; set; } = "";

    [StringLength(2000)]
    [Display(Name = "Hướng dẫn")]
    public string? Instructions
    {
        get; set;
    }

    [DataType(DataType.Date)]
    [Display(Name = "Ngày thực hiện")]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    [DataType(DataType.Time)]
    [Display(Name = "Giờ thực hiện")]
    public TimeOnly Time { get; set; } = new(8, 0);
}
