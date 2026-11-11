using System.ComponentModel.DataAnnotations;

namespace NursingHomeManagement.Models;

public class Invoice : BaseEntity
{
    public int ResidentId
    {
        get; set;
    }

    public Resident Resident { get; set; } = null!;

    [Required, MaxLength(40)]
    public string Number { get; set; } = "";

    public DateOnly BillingPeriod
    {
        get; set;
    }

    public DateTimeOffset? IssuedAt
    {
        get; set;
    }

    public DateOnly DueDate
    {
        get; set;
    }

    public InvoiceStatus Status
    {
        get; set;
    }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
