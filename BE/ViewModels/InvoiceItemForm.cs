using System.ComponentModel.DataAnnotations;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed class InvoiceItemForm
{
    [Required, StringLength(250)]
    [Display(Name = "Nội dung khoản phí")]
    public string Description { get; set; } = "";

    [Range(typeof(decimal), "0.01", "10000")]
    [Display(Name = "Số lượng")]
    public decimal Quantity { get; set; } = 1m;

    [Range(typeof(decimal), "0", "999999999999")]
    [Display(Name = "Đơn giá")]
    public decimal UnitPrice
    {
        get; set;
    }
}
