using System.Globalization;
using System.Text;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.Services.Options;

namespace NursingHome.Services.Helpers;

public enum SettingKind
{
    Text,
    Integer,
    Decimal
}

/// <summary>Một khóa cấu hình Admin được phép sửa (UC-129). Khóa ngoài danh sách này không bao giờ ghi đè được.</summary>
public sealed record SettingDefinition(string Key, string Label, string Group, SettingKind Kind, decimal Min = 0, decimal Max = 0, bool Required = false);

/// <summary>
/// UC-129: danh sách khóa cho phép sửa, kiểm tra giá trị và đọc/ghi vào <see cref="FacilityOptions"/>.
/// Hàm thuần để unit test. Số luôn đọc/ghi theo invariant culture (API chạy locale vi-VN).
/// </summary>
public static class SettingsCatalog
{
    public static readonly IReadOnlyList<SettingDefinition> All =
    [
        new("Facility:Name", "Tên cơ sở", "Thông tin cơ sở", SettingKind.Text, Required: true),
        new("Facility:Address", "Địa chỉ", "Thông tin cơ sở", SettingKind.Text),
        new("Facility:Hotline", "Hotline", "Thông tin cơ sở", SettingKind.Text),
        new("Facility:VisitingHours", "Giờ thăm thân", "Thông tin cơ sở", SettingKind.Text),
        new("Facility:TourSlotCapacity", "Số khách tối đa mỗi buổi tham quan", "Sức chứa", SettingKind.Integer, 1, 50),
        new("Facility:VisitSlotCapacity", "Số khách tối đa mỗi khung giờ thăm", "Sức chứa", SettingKind.Integer, 1, 50),
        new("Facility:VitalAlerts:SystolicHigh", "Huyết áp tâm thu cao từ (mmHg)", "Ngưỡng cảnh báo sinh hiệu", SettingKind.Integer, 120, 260),
        new("Facility:VitalAlerts:SystolicLow", "Huyết áp tâm thu thấp dưới (mmHg)", "Ngưỡng cảnh báo sinh hiệu", SettingKind.Integer, 50, 120),
        new("Facility:VitalAlerts:HeartRateHigh", "Nhịp tim cao từ (lần/phút)", "Ngưỡng cảnh báo sinh hiệu", SettingKind.Integer, 90, 200),
        new("Facility:VitalAlerts:HeartRateLow", "Nhịp tim thấp dưới (lần/phút)", "Ngưỡng cảnh báo sinh hiệu", SettingKind.Integer, 30, 70),
        new("Facility:VitalAlerts:TemperatureHigh", "Nhiệt độ cao từ (°C)", "Ngưỡng cảnh báo sinh hiệu", SettingKind.Decimal, 37, 42),
        new("Facility:VitalAlerts:SpO2Low", "SpO₂ thấp dưới (%)", "Ngưỡng cảnh báo sinh hiệu", SettingKind.Decimal, 80, 98)
    ];

    public const int MaxTextLength = 300;

    public static SettingDefinition? Find(string key) => All.FirstOrDefault(item => item.Key == key);

    public static FacilityOptions Clone(FacilityOptions source) => new()
    {
        Name = source.Name,
        Address = source.Address,
        Hotline = source.Hotline,
        VisitingHours = source.VisitingHours,
        TourSlotCapacity = source.TourSlotCapacity,
        VisitSlotCapacity = source.VisitSlotCapacity,
        VitalAlerts = new VitalAlertOptions
        {
            SystolicHigh = source.VitalAlerts.SystolicHigh,
            SystolicLow = source.VitalAlerts.SystolicLow,
            HeartRateHigh = source.VitalAlerts.HeartRateHigh,
            HeartRateLow = source.VitalAlerts.HeartRateLow,
            TemperatureHigh = source.VitalAlerts.TemperatureHigh,
            SpO2Low = source.VitalAlerts.SpO2Low
        }
    };

    /// <summary>Giá trị hiện tại của khóa, dạng chuỗi invariant.</summary>
    public static string Read(FacilityOptions options, string key) => key switch
    {
        "Facility:Name" => options.Name,
        "Facility:Address" => options.Address ?? "",
        "Facility:Hotline" => options.Hotline ?? "",
        "Facility:VisitingHours" => options.VisitingHours ?? "",
        "Facility:TourSlotCapacity" => Invariant(options.TourSlotCapacity),
        "Facility:VisitSlotCapacity" => Invariant(options.VisitSlotCapacity),
        "Facility:VitalAlerts:SystolicHigh" => Invariant(options.VitalAlerts.SystolicHigh),
        "Facility:VitalAlerts:SystolicLow" => Invariant(options.VitalAlerts.SystolicLow),
        "Facility:VitalAlerts:HeartRateHigh" => Invariant(options.VitalAlerts.HeartRateHigh),
        "Facility:VitalAlerts:HeartRateLow" => Invariant(options.VitalAlerts.HeartRateLow),
        "Facility:VitalAlerts:TemperatureHigh" => Invariant(options.VitalAlerts.TemperatureHigh),
        "Facility:VitalAlerts:SpO2Low" => Invariant(options.VitalAlerts.SpO2Low),
        _ => throw new BusinessRuleException($"Khóa cấu hình không hợp lệ: {key}.")
    };

    /// <summary>Ghi giá trị ĐÃ chuẩn hóa (qua <see cref="Normalize"/>) vào options.</summary>
    public static void Write(FacilityOptions options, string key, string value)
    {
        switch (key)
        {
            case "Facility:Name": options.Name = value; break;
            case "Facility:Address": options.Address = value.Length == 0 ? null : value; break;
            case "Facility:Hotline": options.Hotline = value.Length == 0 ? null : value; break;
            case "Facility:VisitingHours": options.VisitingHours = value.Length == 0 ? null : value; break;
            case "Facility:TourSlotCapacity": options.TourSlotCapacity = ParseInt(value); break;
            case "Facility:VisitSlotCapacity": options.VisitSlotCapacity = ParseInt(value); break;
            case "Facility:VitalAlerts:SystolicHigh": options.VitalAlerts.SystolicHigh = ParseInt(value); break;
            case "Facility:VitalAlerts:SystolicLow": options.VitalAlerts.SystolicLow = ParseInt(value); break;
            case "Facility:VitalAlerts:HeartRateHigh": options.VitalAlerts.HeartRateHigh = ParseInt(value); break;
            case "Facility:VitalAlerts:HeartRateLow": options.VitalAlerts.HeartRateLow = ParseInt(value); break;
            case "Facility:VitalAlerts:TemperatureHigh": options.VitalAlerts.TemperatureHigh = ParseDecimal(value); break;
            case "Facility:VitalAlerts:SpO2Low": options.VitalAlerts.SpO2Low = ParseDecimal(value); break;
            default: throw new BusinessRuleException($"Khóa cấu hình không hợp lệ: {key}.");
        }
    }

    /// <summary>Kiểm tra kiểu, khoảng giá trị và độ dài; trả giá trị chuẩn hóa (cắt khoảng trắng, số theo invariant).</summary>
    public static string Normalize(SettingDefinition definition, string? raw)
    {
        string text = (raw ?? "").Trim();
        switch (definition.Kind)
        {
            case SettingKind.Text:
                if (definition.Required && text.Length == 0)
                {
                    throw new BusinessRuleException($"{definition.Label} không được để trống.");
                }

                if (text.Length > MaxTextLength)
                {
                    throw new BusinessRuleException($"{definition.Label} tối đa {MaxTextLength} ký tự.");
                }

                return text;
            case SettingKind.Integer:
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer))
                {
                    throw new BusinessRuleException($"{definition.Label} phải là số nguyên.");
                }

                CheckRange(definition, integer);
                return Invariant(integer);
            default:
                if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number))
                {
                    throw new BusinessRuleException($"{definition.Label} phải là số.");
                }

                CheckRange(definition, number);
                return Invariant(number);
        }
    }

    /// <summary>Các cặp ngưỡng phải hợp lý với nhau (thấp &lt; cao). Gọi trên options SAU KHI áp toàn bộ thay đổi.</summary>
    public static void ValidateConsistency(FacilityOptions options)
    {
        var vitals = options.VitalAlerts;
        if (vitals.SystolicLow >= vitals.SystolicHigh)
        {
            throw new BusinessRuleException("Ngưỡng huyết áp thấp phải nhỏ hơn ngưỡng huyết áp cao.");
        }

        if (vitals.HeartRateLow >= vitals.HeartRateHigh)
        {
            throw new BusinessRuleException("Ngưỡng nhịp tim thấp phải nhỏ hơn ngưỡng nhịp tim cao.");
        }
    }

    private static void CheckRange(SettingDefinition definition, decimal value)
    {
        if (value < definition.Min || value > definition.Max)
        {
            throw new BusinessRuleException($"{definition.Label} phải từ {Invariant(definition.Min)} đến {Invariant(definition.Max)}.");
        }
    }

    private static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Invariant(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static int ParseInt(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string value) => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
}

/// <summary>UC-119: chọn người nhận thông báo hàng loạt.</summary>
public static class AnnouncementRules
{
    /// <summary>Tối đa số email gửi cho MỘT thông báo (gửi tuần tự trong request); người nhận còn lại vẫn có thông báo trong ứng dụng.</summary>
    public const int MaxEmailRecipients = 100;

    public static IReadOnlyCollection<string> RolesFor(string audience, string? role)
    {
        if (!AnnouncementAudiences.Values.Contains(audience))
        {
            throw new BusinessRuleException("Đối tượng nhận chỉ nhận All, Family, Staff hoặc Role.");
        }

        if (audience == AnnouncementAudiences.Role)
        {
            if (string.IsNullOrWhiteSpace(role) || !Roles.IsValid(role))
            {
                throw new BusinessRuleException("Chọn vai trò nhận thông báo.");
            }

            return [role];
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            throw new BusinessRuleException("Chỉ chọn vai trò khi đối tượng nhận là một vai trò cụ thể.");
        }

        return audience switch
        {
            AnnouncementAudiences.All => Roles.All,
            AnnouncementAudiences.Family => [Roles.Family],
            _ => Roles.Staff
        };
    }

    /// <summary>Thân thông báo trong ứng dụng giới hạn 1000 ký tự (cột Notifications.Body).</summary>
    public static string NotificationBody(string body) => body.Length <= 1000 ? body : body[..999] + "…";
}

/// <summary>UC-19/115: slug, kiểm tra ảnh và điều kiện đăng bài. Hàm thuần để unit test.</summary>
public static class PostRules
{
    public const int MaxSlugLength = 200;

    public const int MaxImages = 30;

    /// <summary>
    /// "Chương trình khám sức khỏe" → "chuong-trinh-kham-suc-khoe". Bỏ dấu tiếng Việt (đ → d), chỉ giữ a-z, 0-9 và gạch nối.
    /// Tiêu đề không còn ký tự nào dùng được → "bai-viet".
    /// </summary>
    public static string Slugify(string title)
    {
        string normalized = title.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        bool pendingDash = false;
        foreach (char c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingDash && builder.Length > 0)
                {
                    builder.Append('-');
                }

                pendingDash = false;
                builder.Append(c);
            }
            else
            {
                pendingDash = true;
            }

            if (builder.Length >= MaxSlugLength)
            {
                break;
            }
        }

        return builder.Length == 0 ? "bai-viet" : builder.ToString().TrimEnd('-');
    }

    /// <summary>Chỉ nhận ảnh do chính hệ thống lưu: "/uploads/..." (local) hoặc Cloudinary. Chặn URL ngoài và scheme lạ (javascript:, data:).</summary>
    public static bool IsTrustedImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 500)
        {
            return false;
        }

        if (url.StartsWith("/uploads/", StringComparison.Ordinal))
        {
            return !url.Contains("..", StringComparison.Ordinal) && !url.Contains('\\');
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && uri.Scheme == Uri.UriSchemeHttps
               && uri.Host.Equals("res.cloudinary.com", StringComparison.OrdinalIgnoreCase);
    }

    public static PostKind ParseKind(string? kind) =>
        Enum.TryParse<PostKind>(kind, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new BusinessRuleException("Loại bài chỉ nhận News hoặc Album.");

    /// <summary>Điều kiện giữ trạng thái "đã đăng": album phải có xác nhận đồng ý và ít nhất một ảnh.</summary>
    public static void ValidatePublished(PostKind kind, bool consentConfirmed, int imageCount)
    {
        if (kind != PostKind.Album)
        {
            return;
        }

        if (!consentConfirmed)
        {
            throw new BusinessRuleException("Album chỉ đăng được khi đã xác nhận có sự đồng ý của cư dân hoặc gia đình cho phép ảnh xuất hiện công khai.");
        }

        if (imageCount == 0)
        {
            throw new BusinessRuleException("Album cần có ít nhất một ảnh trước khi đăng.");
        }
    }
}
