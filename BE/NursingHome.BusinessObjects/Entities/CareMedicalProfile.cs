namespace NursingHome.BusinessObjects.Entities;

public enum ConditionStatus
{
    Active,
    Resolved
}

public enum AllergyType
{
    Drug,
    Food,
    Other
}

public enum AllergySeverity
{
    Mild,
    Moderate,
    Severe
}

public enum DietType
{
    Normal,
    Diabetic,
    LowSalt,
    LowFat,
    Renal,
    Vegetarian,
    Other
}

public enum DietTexture
{
    Normal,
    Soft,
    Minced,
    Pureed,
    Liquid
}

/// <summary>S3-H2 (UC-66): bệnh nền của cư dân.</summary>
public class MedicalCondition
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public Resident Resident { get; set; } = null!;
    public string Name { get; set; } = "";
    public DateOnly? DiagnosedOn { get; set; }
    public ConditionStatus Status { get; set; } = ConditionStatus.Active;
    public string? Notes { get; set; }
    public int RecordedById { get; set; }
    public Account RecordedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>S3-H2 (UC-66, UC-71): dị ứng thuốc / thực phẩm / khác.</summary>
public class Allergy
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public Resident Resident { get; set; } = null!;
    public string Allergen { get; set; } = "";
    public AllergyType Type { get; set; }
    public string? Reaction { get; set; }
    public AllergySeverity Severity { get; set; }
    public int RecordedById { get; set; }
    public Account RecordedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>S3-H2 (UC-82): chế độ ăn. Tối đa 1 bản ghi EndDate IS NULL cho mỗi cư dân.</summary>
public class DietPlan
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public Resident Resident { get; set; } = null!;
    public DietType DietType { get; set; }
    public DietTexture Texture { get; set; } = DietTexture.Normal;
    public string? Restrictions { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int PrescribedById { get; set; }
    public Account PrescribedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
