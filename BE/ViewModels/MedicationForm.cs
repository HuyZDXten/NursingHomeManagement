using System.ComponentModel.DataAnnotations;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed class MedicationForm
{
    [Range(1, int.MaxValue)]
    [Display(Name = "Cư dân")]
    public int ResidentId
    {
        get; set;
    }

    [Required, StringLength(200)]
    [Display(Name = "Tên thuốc")]
    public string MedicationName { get; set; } = "";

    [Required, StringLength(100)]
    [Display(Name = "Liều đã phê duyệt")]
    public string Dose { get; set; } = "";

    [Required, StringLength(100)]
    [Display(Name = "Đường dùng")]
    public string Route { get; set; } = "";

    [DataType(DataType.Date)]
    [Display(Name = "Ngày bắt đầu")]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    [DataType(DataType.Date)]
    [Display(Name = "Ngày kết thúc")]
    public DateOnly? EndDate
    {
        get; set;
    }

    [DataType(DataType.Time)]
    [Display(Name = "Giờ dùng mỗi ngày")]
    public TimeOnly ScheduledTime { get; set; } = new(8, 0);

    [Required, StringLength(200)]
    [Display(Name = "Tham chiếu phê duyệt chuyên môn")]
    public string ApprovalReference { get; set; } = "";

    [StringLength(2000)]
    [Display(Name = "Hướng dẫn đã phê duyệt")]
    public string? Instructions
    {
        get; set;
    }
}
