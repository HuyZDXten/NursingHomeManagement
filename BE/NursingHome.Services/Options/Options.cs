namespace NursingHome.Services.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 7;
}

public class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    /// <summary>None, Auto, SslOnConnect, StartTls, StartTlsWhenAvailable (MailKit).</summary>
    public string SmtpSecurity { get; set; } = "StartTls";
    public string SmtpUser { get; set; } = "";
    public string SmtpPassword { get; set; } = "";
    public string FromName { get; set; } = "Nursing Home Management";
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SmtpUser) && !string.IsNullOrWhiteSpace(SmtpPassword);
}

/// <summary>
/// Một kênh payOS duy nhất của cơ sở (khác StreetBiz: nhiều người nhận tiền). Khóa đặt trong
/// user-secrets hoặc appsettings.Development.json, không commit.
/// </summary>
public class PayOsOptions
{
    public const string SectionName = "PayOs";

    public bool Enabled { get; set; }
    public string ApiBaseUrl { get; set; } = "https://api-merchant.payos.vn";
    public string ClientId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ChecksumKey { get; set; } = "";
    public string ReturnUrl { get; set; } = "http://localhost:5173/family/payments/result";
    public string CancelUrl { get; set; } = "http://localhost:5173/family/payments/result";
    public int LinkLifetimeMinutes { get; set; } = 30;
    public int SyncIntervalSeconds { get; set; } = 60;

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ChecksumKey);
}

public class FacilityOptions
{
    public const string SectionName = "Facility";

    public string Name { get; set; } = "Viện Dưỡng Lão";
    public string? Address { get; set; }
    public string? Hotline { get; set; }
    public string? VisitingHours { get; set; }

    /// <summary>Ngưỡng cảnh báo mặc định (UC-65). Chỉ để báo nhân viên xem lại, không phải chẩn đoán.</summary>
    public VitalAlertOptions VitalAlerts { get; set; } = new();
}

public class VitalAlertOptions
{
    public int SystolicHigh { get; set; } = 160;
    public int SystolicLow { get; set; } = 90;
    public int HeartRateHigh { get; set; } = 120;
    public int HeartRateLow { get; set; } = 50;
    public decimal TemperatureHigh { get; set; } = 38.0m;
    public decimal SpO2Low { get; set; } = 92m;
}
