using System.ComponentModel.DataAnnotations;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed class MedicationLogForm
{
    [Range(1, int.MaxValue)]
    [Display(Name = "Lịch thuốc")]
    public int ScheduleId
    {
        get; set;
    }

    [DataType(DataType.Date)]
    [Display(Name = "Ngày dùng")]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    [EnumDataType(typeof(MedicationStatus))]
    [Display(Name = "Trạng thái")]
    public MedicationStatus Status
    {
        get; set;
    }

    [StringLength(2000)]
    [Display(Name = "Ghi chú / lý do")]
    public string? Notes
    {
        get; set;
    }
}
