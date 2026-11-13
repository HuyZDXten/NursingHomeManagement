using NursingHome.BusinessObjects.Enums;

namespace NursingHome.BusinessObjects.Entities;

public class Room
{
    public int Id { get; set; }

    public string Number { get; set; } = "";

    public int? Floor { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<Bed> Beds { get; set; } = new List<Bed>();
}

public class Bed
{
    public int Id { get; set; }

    public int RoomId { get; set; }

    public Room Room { get; set; } = null!;

    public string Number { get; set; } = "";

    public BedStatus Status { get; set; } = BedStatus.Available;

    public byte[] RowVersion { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<BedAssignment> Assignments { get; set; } = new List<BedAssignment>();
}

public class BedAssignment
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public Resident Resident { get; set; } = null!;

    public int BedId { get; set; }

    public Bed Bed { get; set; } = null!;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? EndedAt { get; set; }

    public int AssignedById { get; set; }

    public Account AssignedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
