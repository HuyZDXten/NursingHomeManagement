using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Care;

public class CarePlanItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Instructions { get; set; }
    public TimeOnly TimeOfDay { get; set; }
    public string DaysOfWeek { get; set; } = "1111111";
    public int? DefaultAssigneeId { get; set; }
    public string? DefaultAssigneeName { get; set; }
}

public class CarePlanDto
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public string Title { get; set; } = "";
    public string? Goals { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; }
    public string CreatedByName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<CarePlanItemDto> Items { get; set; } = [];
}

public class SaveCarePlanItemRequest
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(1000)]
    public string? Instructions { get; set; }

    [Required]
    public TimeOnly TimeOfDay { get; set; }

    /// <summary>7 ký tự '0'/'1' từ Thứ Hai (index 0) đến Chủ Nhật (index 6).</summary>
    [Required, RegularExpression("^[01]{7}$", ErrorMessage = "DaysOfWeek phải gồm đúng 7 ký tự 0 hoặc 1.")]
    public string DaysOfWeek { get; set; } = "1111111";

    public int? DefaultAssigneeId { get; set; }
}

public class SaveCarePlanRequest
{
    [Range(1, int.MaxValue)]
    public int ResidentId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(1000)]
    public string? Goals { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [MinLength(1, ErrorMessage = "Kế hoạch cần ít nhất 1 mục chăm sóc.")]
    public List<SaveCarePlanItemRequest> Items { get; set; } = [];
}

public class GenerateTasksResultDto
{
    public int CreatedCount { get; set; }
    public int SkippedUnassignedCount { get; set; }
}

public class DailyCareLogDto
{
    public long Id { get; set; }
    public int ResidentId { get; set; }
    public DateOnly LogDate { get; set; }
    public string Category { get; set; } = "";
    public string Level { get; set; } = "";
    public string? Note { get; set; }
    public string RecordedByName { get; set; } = "";
    public DateTime RecordedAt { get; set; }
    public bool ShareWithFamily { get; set; }
}

public class CreateDailyCareLogRequest
{
    [Required]
    public DateOnly LogDate { get; set; }

    /// <summary>Bathing, Toileting, Mobility, Sleep, Mood hoặc Other.</summary>
    [Required]
    public string Category { get; set; } = "";

    /// <summary>Independent, NeedsHelp, Dependent, Good, Fair hoặc Poor.</summary>
    [Required]
    public string Level { get; set; } = "";

    [MaxLength(500)]
    public string? Note { get; set; }

    public bool ShareWithFamily { get; set; } = true;
}

public class MealLogDto
{
    public long Id { get; set; }
    public int ResidentId { get; set; }
    public DateOnly MealDate { get; set; }
    public string Meal { get; set; } = "";
    public int IntakePercent { get; set; }
    public int? FluidMl { get; set; }
    public string? Note { get; set; }
    public string RecordedByName { get; set; } = "";
    public DateTime RecordedAt { get; set; }
    public bool ShareWithFamily { get; set; }
}

public class UpsertMealLogRequest
{
    [Required]
    public DateOnly MealDate { get; set; }

    /// <summary>Breakfast, Lunch, Dinner hoặc Snack.</summary>
    [Required]
    public string Meal { get; set; } = "";

    [Range(0, 100)]
    public int IntakePercent { get; set; }

    [Range(0, 5000)]
    public int? FluidMl { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    public bool ShareWithFamily { get; set; } = true;
}
