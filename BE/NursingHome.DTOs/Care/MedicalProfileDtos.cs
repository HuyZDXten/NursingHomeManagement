using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Care;

/// <summary>S3-H2 (UC-66): bệnh nền.</summary>
public class ConditionDto
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public string Name { get; set; } = "";
    public DateOnly? DiagnosedOn { get; set; }
    public string Status { get; set; } = "";
    public string? Notes { get; set; }
    public string RecordedByName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class SaveConditionRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    public DateOnly? DiagnosedOn { get; set; }

    /// <summary>Active hoặc Resolved.</summary>
    [Required]
    public string Status { get; set; } = "Active";

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>S3-H2 (UC-66, UC-71): dị ứng.</summary>
public class AllergyDto
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public string Allergen { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Reaction { get; set; }
    public string Severity { get; set; } = "";
    public string RecordedByName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class SaveAllergyRequest
{
    [Required, MaxLength(150)]
    public string Allergen { get; set; } = "";

    /// <summary>Drug, Food hoặc Other.</summary>
    [Required]
    public string Type { get; set; } = "Drug";

    [MaxLength(500)]
    public string? Reaction { get; set; }

    /// <summary>Mild, Moderate hoặc Severe.</summary>
    [Required]
    public string Severity { get; set; } = "Moderate";
}

/// <summary>S3-H2 (UC-82): chế độ ăn.</summary>
public class DietPlanDto
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public string DietType { get; set; } = "";
    public string Texture { get; set; } = "";
    public string? Restrictions { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string PrescribedByName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class SaveDietPlanRequest
{
    /// <summary>Normal, Diabetic, LowSalt, LowFat, Renal, Vegetarian hoặc Other.</summary>
    [Required]
    public string DietType { get; set; } = "Normal";

    /// <summary>Normal, Soft, Minced, Pureed hoặc Liquid.</summary>
    [Required]
    public string Texture { get; set; } = "Normal";

    [MaxLength(1000)]
    public string? Restrictions { get; set; }

    public DateOnly? StartDate { get; set; }
}

/// <summary>Tổng hợp hồ sơ y tế của cư dân (dùng cho cả staff và trang người thân).</summary>
public class MedicalProfileDto
{
    public List<ConditionDto> Conditions { get; set; } = [];
    public List<AllergyDto> Allergies { get; set; } = [];
    public DietPlanDto? ActiveDiet { get; set; }
    public List<DietPlanDto> DietHistory { get; set; } = [];
}

public class CheckMedicationRequest
{
    [Range(1, int.MaxValue)]
    public int ResidentId { get; set; }

    [Required, MaxLength(200)]
    public string MedicationName { get; set; } = "";
}

/// <summary>Cảnh báo theo quy tắc so khớp tên thuốc khi kê/nhập lịch.</summary>
public class MedicationWarningDto
{
    /// <summary>Allergy hoặc Duplicate.</summary>
    public string Kind { get; set; } = "";
    public string Message { get; set; } = "";
}
