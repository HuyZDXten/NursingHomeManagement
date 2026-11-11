using System.ComponentModel.DataAnnotations;

namespace NursingHomeManagement.Models;

public class Bed : BaseEntity
{
    public int RoomId
    {
        get; set;
    }

    public Room Room { get; set; } = null!;

    [Required, MaxLength(30)]
    public string Number { get; set; } = "";

    public BedStatus Status { get; set; } = BedStatus.Available;

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}
