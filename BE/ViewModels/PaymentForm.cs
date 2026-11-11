using System.ComponentModel.DataAnnotations;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed class PaymentForm
{
    [Range(typeof(decimal), "0.01", "999999999999")]
    [Display(Name = "Số tiền thanh toán")]
    public decimal Amount
    {
        get; set;
    }

    [Required, StringLength(100)]
    [Display(Name = "Mã tham chiếu thanh toán")]
    public string Reference { get; set; } = Guid.NewGuid().ToString("N");
}
