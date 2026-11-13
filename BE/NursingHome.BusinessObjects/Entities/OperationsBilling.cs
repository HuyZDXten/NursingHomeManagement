namespace NursingHome.BusinessObjects.Entities;

// MODULE VẬN HÀNH & TÀI CHÍNH (Viên) — S3-V5 dịch vụ phát sinh, nhắc nợ (UC-85, 86, 88, 97, 98, 118).
// Bảng: Database/01_Schema.sql mục 9.4 (ExtraServices, ServiceUsages, InvoiceReminders).

/// <summary>Danh mục dịch vụ phát sinh (giặt ủi, vật lý trị liệu…). Đổi giá không ảnh hưởng lần sử dụng đã ghi.</summary>
public class ExtraService
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    /// <summary>Đơn vị tính: kg, buổi, lần, chuyến…</summary>
    public string Unit { get; set; } = "";

    public decimal UnitPrice { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Một lần sử dụng dịch vụ phát sinh của cư dân. UnitPrice chốt lúc ghi. InvoiceItemId có giá trị khi đã vào hóa đơn —
/// từ đó không sửa/xóa được (hủy hóa đơn thì gỡ ra để vào hóa đơn kỳ sau).
/// </summary>
public class ServiceUsage
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public Resident Resident { get; set; } = null!;

    public int ExtraServiceId { get; set; }

    public ExtraService ExtraService { get; set; } = null!;

    /// <summary>Ngày sử dụng (giờ cơ sở).</summary>
    public DateOnly UsedOn { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public string? Note { get; set; }

    public int RecordedById { get; set; }

    public Account RecordedBy { get; set; } = null!;

    public int? InvoiceItemId { get; set; }

    public InvoiceItem? InvoiceItem { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>Mốc nhắc nợ, theo số ngày từ hôm nay tới hạn thanh toán.</summary>
public enum ReminderStage
{
    /// <summary>Còn 0–3 ngày tới hạn.</summary>
    Before3Days,

    /// <summary>Quá hạn 1–6 ngày.</summary>
    Overdue,

    /// <summary>Quá hạn từ 7 ngày.</summary>
    Overdue7
}

/// <summary>Đã nhắc một mốc cho một hóa đơn (UQ_InvoiceReminders: mỗi mốc một lần).</summary>
public class InvoiceReminder
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    public ReminderStage Stage { get; set; }

    public DateTime SentAt { get; set; }
}
