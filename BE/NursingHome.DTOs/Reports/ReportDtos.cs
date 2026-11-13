namespace NursingHome.DTOs.Reports;

// MODULE VẬN HÀNH (Viên) — S3-V3 báo cáo (UC-122, 123, 124; UC-125 sau S3-H1).

/// <summary>File Excel trả về cho controller (File(content, ContentType, FileName)).</summary>
public sealed record ExportFile(byte[] Content, string FileName)
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

public class RevenueMonthDto
{
    public int Month { get; set; }
    public decimal Collected { get; set; }
    public decimal CollectedOnline { get; set; }
    public decimal CollectedManual { get; set; }
    public int PaymentCount { get; set; }
}

public class DebtRowDto
{
    public int InvoiceId { get; set; }
    public string Number { get; set; } = "";
    public string ResidentCode { get; set; } = "";
    public string ResidentName { get; set; } = "";
    public DateOnly BillingPeriod { get; set; }
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
    public decimal Paid { get; set; }
    public decimal Remaining { get; set; }

    /// <summary>0 nếu chưa tới hạn.</summary>
    public int DaysOverdue { get; set; }
}

/// <summary>UC-123: thực thu theo tháng (đủ 12 tháng) + công nợ hiện tại.</summary>
public class RevenueReportDto
{
    public int Year { get; set; }
    public List<RevenueMonthDto> Months { get; set; } = [];
    public decimal TotalCollected { get; set; }
    public decimal TotalDebt { get; set; }
    public List<DebtRowDto> Debts { get; set; } = [];
}

public class OccupancyMonthDto
{
    public int Month { get; set; }
    public int OccupiedBedDays { get; set; }
    public int AvailableBedDays { get; set; }

    /// <summary>% làm tròn 1 chữ số; null nếu tháng chưa tới.</summary>
    public decimal? Percent { get; set; }
}

/// <summary>UC-122: công suất giường theo tháng.</summary>
public class OccupancyReportDto
{
    public int Year { get; set; }
    public List<OccupancyMonthDto> Months { get; set; } = [];

    /// <summary>Cả năm tới hôm nay.</summary>
    public decimal? AveragePercent { get; set; }
}

public class AdherenceRowDto
{
    public int ResidentId { get; set; }
    public string Code { get; set; } = "";
    public string FullName { get; set; } = "";
    public int Taken { get; set; }
    public int Missed { get; set; }
    public int Refused { get; set; }
    public int Total { get; set; }

    /// <summary>% Taken / (Taken + Missed + Refused), 1 chữ số.</summary>
    public decimal TakenRate { get; set; }
}

/// <summary>UC-124: tuân thủ dùng thuốc theo cư dân.</summary>
public class AdherenceReportDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public List<AdherenceRowDto> Rows { get; set; } = [];
    public decimal? OverallTakenRate { get; set; }
}
