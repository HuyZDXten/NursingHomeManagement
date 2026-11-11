using System.ComponentModel.DataAnnotations;

namespace NursingHomeManagement.Models;

public class Payment : BaseEntity
{
    public int InvoiceId
    {
        get; set;
    }

    public Invoice Invoice { get; set; } = null!;

    public decimal Amount
    {
        get; set;
    }

    public DateTimeOffset PaidAt { get; set; } = DateTimeOffset.UtcNow;

    [Required, MaxLength(100)]
    public string Reference { get; set; } = "";

    [Required]
    public string RecordedById { get; set; } = "";

    public ApplicationUser RecordedBy { get; set; } = null!;
}
