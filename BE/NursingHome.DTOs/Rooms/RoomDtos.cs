using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Rooms;

public class RoomDto
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public int? Floor { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public List<BedDto> Beds { get; set; } = [];
}

public class BedDto
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public string Number { get; set; } = "";
    public string Status { get; set; } = "";
    public int? ResidentId { get; set; }
    public string? ResidentName { get; set; }
    public string? ResidentCode { get; set; }
}

public class SaveRoomRequest
{
    [Required, MaxLength(30)]
    public string Number { get; set; } = "";

    [Range(-5, 200)]
    public int? Floor { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class SaveBedRequest
{
    [Required, MaxLength(30)]
    public string Number { get; set; } = "";

    public bool Maintenance { get; set; }
}

public class AssignBedRequest
{
    [Range(1, int.MaxValue)]
    public int ResidentId { get; set; }

    [Range(1, int.MaxValue)]
    public int BedId { get; set; }
}

public class BedAssignmentDto
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public string ResidentName { get; set; } = "";
    public string RoomNumber { get; set; } = "";
    public string BedNumber { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string AssignedByName { get; set; } = "";
}
