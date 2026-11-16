using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Care;

/// <summary>S3-H3: thông tin một khung giờ thăm trong ngày.</summary>
public class VisitSlotDto
{
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
    public int Capacity { get; set; }
    public int Remaining { get; set; }
    public bool IsPast { get; set; }
}

public class BookVisitRequest
{
    [Range(1, int.MaxValue)]
    public int ResidentId { get; set; }

    [Required]
    public DateOnly VisitDate { get; set; }

    [Required]
    public TimeOnly SlotStart { get; set; }

    [Range(1, 5)]
    public int GuestCount { get; set; } = 1;

    [MaxLength(500)]
    public string? GuestNames { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

public class VisitBookingDto
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public string ResidentName { get; set; } = "";
    public string ResidentCode { get; set; } = "";
    public int FamilyAccountId { get; set; }
    public string FamilyName { get; set; } = "";
    public string? FamilyPhone { get; set; }
    public DateOnly VisitDate { get; set; }
    public TimeOnly SlotStart { get; set; }
    public TimeOnly SlotEnd { get; set; }
    public int GuestCount { get; set; }
    public string? GuestNames { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "";
    public string? ReviewedByName { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReviewVisitRequest
{
    [MaxLength(500)]
    public string? Note { get; set; }
}
