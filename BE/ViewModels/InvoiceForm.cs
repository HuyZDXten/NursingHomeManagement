using System.ComponentModel.DataAnnotations;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed class InvoiceForm
{
    [Range(1, int.MaxValue)]
    [Display(Name = "Cư dân")]
    public int ResidentId
    {
        get; set;
    }

    [DataType(DataType.Date)]
    [Display(Name = "Kỳ hóa đơn (ngày đầu tháng)")]
    public DateOnly BillingPeriod { get; set; } = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

    [DataType(DataType.Date)]
    [Display(Name = "Hạn thanh toán")]
    public DateOnly DueDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
}
