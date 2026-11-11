using System.ComponentModel.DataAnnotations;

namespace NursingHomeManagement.Models;

public class Room : BaseEntity
{
    [Required, MaxLength(30)]
    public string Number { get; set; } = "";

    [MaxLength(100)]
    public string? Description
    {
        get; set;
    }

    public bool IsActive { get; set; } = true;

    public ICollection<Bed> Beds { get; set; } = new List<Bed>();
}
