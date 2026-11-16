namespace NursingHome.BusinessObjects.Entities;

public enum ReminderKind
{
    DueSoon,
    Overdue
}

public enum DailyCareCategory
{
    Bathing,
    Toileting,
    Mobility,
    Sleep,
    Mood,
    Other
}

public enum DailyCareLevel
{
    Independent,
    NeedsHelp,
    Dependent,
    Good,
    Fair,
    Poor
}

public enum MealType
{
    Breakfast,
    Lunch,
    Dinner,
    Snack
}

/// <summary>S3-H4 (UC-51): kế hoạch chăm sóc định kỳ cho cư dân.</summary>
public class CarePlan
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public Resident Resident { get; set; } = null!;
    public string Title { get; set; } = "";
    public string? Goals { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public int CreatedById { get; set; }
    public Account CreatedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public ICollection<CarePlanItem> Items { get; set; } = new List<CarePlanItem>();
}

/// <summary>S3-H4: từng đầu việc lặp lại trong CarePlan.</summary>
public class CarePlanItem
{
    public int Id { get; set; }
    public int CarePlanId { get; set; }
    public CarePlan CarePlan { get; set; } = null!;
    public string Title { get; set; } = "";
    public string? Instructions { get; set; }
    public TimeOnly TimeOfDay { get; set; }

    /// <summary>7 ký tự T2..CN, '1' = có sinh việc ngày đó.</summary>
    public string DaysOfWeek { get; set; } = "1111111";

    public int? DefaultAssigneeId { get; set; }
    public Account? DefaultAssignee { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>S3-H4: bảng nối chống sinh trùng CareTask từ CarePlanItem theo ngày.</summary>
public class GeneratedCareTask
{
    public int CarePlanItemId { get; set; }
    public DateOnly ForDate { get; set; }
    public int CareTaskId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>S3-H4: đánh dấu đã gửi thông báo nhắc việc / quá hạn (chỉ gửi 1 lần).</summary>
public class TaskReminder
{
    public int CareTaskId { get; set; }
    public ReminderKind Kind { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

/// <summary>S3-H4 (UC-56): ghi nhận ADL sinh hoạt hằng ngày.</summary>
public class DailyCareLog
{
    public long Id { get; set; }
    public int ResidentId { get; set; }
    public Resident Resident { get; set; } = null!;
    public DateOnly LogDate { get; set; }
    public DailyCareCategory Category { get; set; }
    public DailyCareLevel Level { get; set; }
    public string? Note { get; set; }
    public int RecordedById { get; set; }
    public Account RecordedBy { get; set; } = null!;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public bool ShareWithFamily { get; set; } = true;
}

/// <summary>S3-H4 (UC-57): ghi nhận bữa ăn &amp; lượng nước (tối đa 1 bản ghi/bữa/ngày/cư dân).</summary>
public class MealLog
{
    public long Id { get; set; }
    public int ResidentId { get; set; }
    public Resident Resident { get; set; } = null!;
    public DateOnly MealDate { get; set; }
    public MealType Meal { get; set; }
    public int IntakePercent { get; set; }
    public int? FluidMl { get; set; }
    public string? Note { get; set; }
    public int RecordedById { get; set; }
    public Account RecordedBy { get; set; } = null!;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public bool ShareWithFamily { get; set; } = true;
}
