using System.ComponentModel.DataAnnotations;

namespace NursingHomeManagement.Models;

public class InvoiceItem : BaseEntity
{
    public int InvoiceId
    {
        get; set;
    }

    public Invoice Invoice { get; set; } = null!;

    [Required, MaxLength(250)]
    public string Description { get; set; } = "";

    public decimal Quantity { get; set; } = 1;

    public decimal UnitPrice
    {
        get; set;
    }
}
