using System.ComponentModel.DataAnnotations;

namespace NursingHomeManagement.Models;

public class ServicePackage : BaseEntity
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = "";

    [Range(typeof(decimal), "0", "999999999999")]
    public decimal MonthlyPrice
    {
        get; set;
    }

    [MaxLength(2000)]
    public string? Description
    {
        get; set;
    }

    public bool IsActive { get; set; } = true;
}
