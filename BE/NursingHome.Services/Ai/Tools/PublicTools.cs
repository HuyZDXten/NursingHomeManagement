using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Admissions;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Ai.Tools;

// ======================================================================================================
// S4-V1 — bộ tool của chatbot tư vấn CÔNG KHAI (khách chưa đăng nhập). Chỉ trả dữ liệu công khai:
// gói dịch vụ, số giường trống, giờ thăm, tài liệu IsPublic, chỗ tham quan còn trống. Không đọc dữ liệu cư dân.
// Mỗi kết quả có trường "summary" (tiếng Việt) để provider Fake dùng khi demo không có key.
// ======================================================================================================

/// <summary>
/// Ngữ cảnh một lượt chat công khai. Tool ghi dữ liệu (tạo yêu cầu, đặt tham quan) chỉ chạy khi model gửi
/// <c>confirmed = true</c> VÀ tin vừa gửi của khách là lời xác nhận — model không tự quyết tạo bản ghi.
/// </summary>
public sealed partial class PublicChatContext(string lastUserMessage)
{
    public string LastUserMessage { get; } = lastUserMessage;

    public bool UserConfirmed => Affirmation().IsMatch(LastUserMessage.ToLowerInvariant());

    // Không gồm "có", "được", "dạ": quá chung ("Có giường trống không?").
    [GeneratedRegex(@"(^|\W)(đồng ý|xác nhận|ok|okay|đúng rồi|đúng vậy|vâng|chốt|gửi đi|đặt đi)(\W|$)")]
    private static partial Regex Affirmation();
}

public static class PublicTools
{
    public static IReadOnlyList<IAiTool> Create(PublicChatContext context, IAiRepository ai, IBillingService billing,
        IDashboardService dashboard, IAdmissionService admissions) =>
    [
        new SearchKnowledgeTool(ai, publicOnly: true),
        new ListPackagesTool(billing),
        new BedAvailabilityTool(dashboard),
        new TourSlotsTool(admissions),
        new CreateInquiryTool(context, admissions),
        new BookTourTool(context, admissions)
    ];

    internal static string Money(decimal value) => $"{value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} đ";

    /// <summary>Kiểm DataAnnotations của DTO (tool gọi service trực tiếp, không qua model binding).</summary>
    internal static string? Validate(object request)
    {
        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true)
            ? null
            : string.Join(" ", results.Select(result => result.ErrorMessage));
    }
}

/// <summary>Tra tài liệu tri thức (FAQ, quy trình nhập viện, giờ thăm…). Huy dùng lại với publicOnly = false.</summary>
public sealed partial class SearchKnowledgeTool(IAiRepository ai, bool publicOnly) : IAiTool
{
    private static readonly HashSet<string> StopWords =
    [
        "và", "của", "có", "không", "là", "cho", "tôi", "bạn", "gì", "nào", "thế", "như", "được", "các", "những", "một",
        "khi", "thì", "ạ", "ơi", "vậy", "muốn", "hỏi", "xin", "cần", "về", "ở", "với", "bao", "nhiêu", "mình", "em", "anh", "chị"
    ];

    public AiToolDefinition Definition { get; } = new("search_knowledge",
        "Tra tài liệu của cơ sở: quy trình nhập viện, giờ thăm và quy định, chính sách thanh toán, dịch vụ y tế, câu hỏi thường gặp.",
        AiToolJson.Schema(("query", "string", "Câu hỏi hoặc từ khóa tiếng Việt", true)));

    public async Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var query = AiToolJson.String(arguments, "query") ?? "";
        var docs = await ai.SearchKnowledgeAsync(Keywords(query), publicOnly, take: 3);
        if (docs.Count == 0)
        {
            return AiToolJson.Serialize(new { documents = Array.Empty<object>(), summary = "Tôi chưa có tài liệu về nội dung này. Bạn vui lòng gọi hotline để được giải đáp." });
        }

        return AiToolJson.Serialize(new
        {
            documents = docs.Select(doc => new { doc.Title, Content = doc.Content.Length <= 1500 ? doc.Content : doc.Content[..1500] }),
            summary = $"{docs[0].Title}: {(docs[0].Content.Length <= 400 ? docs[0].Content : docs[0].Content[..400] + "…")}"
        });
    }

    public static List<string> Keywords(string query) =>
        Words().Matches(query.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(word => word.Length >= 2 && !StopWords.Contains(word))
            .Distinct()
            .Take(12)
            .ToList();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Words();
}

public sealed class ListPackagesTool(IBillingService billing) : IAiTool
{
    public AiToolDefinition Definition { get; } = new("list_packages",
        "Danh sách gói dịch vụ đang bán và giá theo tháng (VND). Luôn dùng tool này khi khách hỏi giá.",
        AiToolJson.Schema());

    public async Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var packages = await billing.ListPackagesAsync(activeOnly: true);
        return AiToolJson.Serialize(new
        {
            packages = packages.Select(item => new { item.Name, item.MonthlyPrice, item.Description }),
            summary = packages.Count == 0
                ? "Hiện chưa có gói dịch vụ đang bán."
                : "Các gói dịch vụ: " + string.Join("; ", packages.Select(item => $"{item.Name} {PublicTools.Money(item.MonthlyPrice)}/tháng")) + "."
        });
    }
}

public sealed class BedAvailabilityTool(IDashboardService dashboard) : IAiTool
{
    public AiToolDefinition Definition { get; } = new("get_bed_availability",
        "Số giường còn trống hiện tại, giờ thăm, địa chỉ và hotline của cơ sở.",
        AiToolJson.Schema());

    public async Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var overview = await dashboard.GetPublicOverviewAsync();
        return AiToolJson.Serialize(new
        {
            overview.AvailableBeds,
            overview.TotalBeds,
            overview.VisitingHours,
            overview.Address,
            overview.Hotline,
            summary = $"Hiện còn {overview.AvailableBeds}/{overview.TotalBeds} giường trống. Giờ thăm: {overview.VisitingHours}. Hotline {overview.Hotline}."
        });
    }
}

public sealed class TourSlotsTool(IAdmissionService admissions) : IAiTool
{
    public AiToolDefinition Definition { get; } = new("get_tour_slots",
        "Các buổi tham quan còn chỗ (Morning 08:00–11:00, Afternoon 14:00–17:00) trong khoảng ngày; mặc định 14 ngày tới.",
        AiToolJson.Schema(("from", "string", "Ngày bắt đầu yyyy-MM-dd (tùy chọn)", false), ("to", "string", "Ngày kết thúc yyyy-MM-dd (tùy chọn)", false)));

    public async Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        DateOnly? from = DateOnly.TryParse(AiToolJson.String(arguments, "from"), CultureInfo.InvariantCulture, out var start) ? start : null;
        DateOnly? to = DateOnly.TryParse(AiToolJson.String(arguments, "to"), CultureInfo.InvariantCulture, out var end) ? end : from?.AddDays(13);
        var open = (await admissions.GetAvailabilityAsync(from, to)).Where(slot => slot.Remaining > 0).Take(10).ToList();
        return AiToolJson.Serialize(new
        {
            slots = open.Select(slot => new { date = slot.Date.ToString("yyyy-MM-dd"), slot.Slot, slot.Remaining }),
            summary = open.Count == 0
                ? "Các buổi tham quan sắp tới đã kín chỗ."
                : "Buổi còn chỗ: " + string.Join("; ", open.Take(5).Select(slot =>
                    $"{slot.Date:dd/MM} {(slot.Slot == nameof(TourSlot.Morning) ? "sáng" : "chiều")} (còn {slot.Remaining})")) + "."
        });
    }
}

public sealed class CreateInquiryTool(PublicChatContext context, IAdmissionService admissions) : IAiTool
{
    public AiToolDefinition Definition { get; } = new("create_inquiry",
        "Gửi yêu cầu tư vấn để lễ tân gọi lại. CHỈ gọi với confirmed=true sau khi đã đọc lại họ tên + số điện thoại và khách trả lời đồng ý.",
        AiToolJson.Schema(
            ("fullName", "string", "Họ tên người liên hệ", true),
            ("phone", "string", "Số điện thoại", true),
            ("careNeeds", "string", "Nhu cầu chăm sóc (tùy chọn)", false),
            ("elderName", "string", "Tên người cao tuổi (tùy chọn)", false),
            ("confirmed", "boolean", "true khi khách đã xác nhận thông tin", true)));

    public async Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var request = new CreateInquiryRequest
        {
            FullName = (AiToolJson.String(arguments, "fullName") ?? "").Trim(),
            Phone = (AiToolJson.String(arguments, "phone") ?? "").Trim(),
            CareNeeds = AiToolJson.String(arguments, "careNeeds")?.Trim(),
            ElderName = AiToolJson.String(arguments, "elderName")?.Trim()
        };
        if (PublicTools.Validate(request) is { } error)
        {
            throw new BusinessRuleException($"Thông tin chưa hợp lệ: {error}");
        }

        if (!AiToolJson.Bool(arguments, "confirmed") || !context.UserConfirmed)
        {
            return AiToolJson.Serialize(new
            {
                needsConfirmation = true,
                summary = $"Vui lòng xác nhận: họ tên {request.FullName}, số điện thoại {request.Phone}. Trả lời \"đồng ý\" để gửi yêu cầu tư vấn."
            });
        }

        var created = await admissions.CreateInquiryAsync(request, InquirySource.Chatbot);
        return AiToolJson.Serialize(new
        {
            created = true,
            inquiryId = created.Id,
            summary = $"Đã gửi yêu cầu tư vấn. Lễ tân sẽ gọi lại số {created.Phone} trong giờ hành chính."
        });
    }
}

public sealed class BookTourTool(PublicChatContext context, IAdmissionService admissions) : IAiTool
{
    public AiToolDefinition Definition { get; } = new("book_tour",
        "Đặt lịch tham quan cơ sở. Kiểm chỗ bằng get_tour_slots trước; CHỈ gọi với confirmed=true sau khi đọc lại thông tin và khách đồng ý.",
        AiToolJson.Schema(
            ("fullName", "string", "Họ tên người đặt", true),
            ("phone", "string", "Số điện thoại", true),
            ("date", "string", "Ngày tham quan yyyy-MM-dd", true),
            ("slot", "string", "Morning hoặc Afternoon", true),
            ("guestCount", "integer", "Số khách (1–10)", true),
            ("confirmed", "boolean", "true khi khách đã xác nhận thông tin", true)));

    public async Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!DateOnly.TryParse(AiToolJson.String(arguments, "date"), CultureInfo.InvariantCulture, out var date))
        {
            throw new BusinessRuleException("Ngày tham quan phải có dạng yyyy-MM-dd.");
        }

        var request = new BookTourRequest
        {
            FullName = (AiToolJson.String(arguments, "fullName") ?? "").Trim(),
            Phone = (AiToolJson.String(arguments, "phone") ?? "").Trim(),
            VisitDate = date,
            TimeSlot = AiToolJson.String(arguments, "slot") ?? "",
            GuestCount = AiToolJson.Int(arguments, "guestCount") ?? 1,
            Note = "Đặt qua chatbot."
        };
        if (PublicTools.Validate(request) is { } error)
        {
            throw new BusinessRuleException($"Thông tin chưa hợp lệ: {error}");
        }

        var slotLabel = request.TimeSlot == nameof(TourSlot.Morning) ? "buổi sáng" : "buổi chiều";
        if (!AiToolJson.Bool(arguments, "confirmed") || !context.UserConfirmed)
        {
            return AiToolJson.Serialize(new
            {
                needsConfirmation = true,
                summary = $"Vui lòng xác nhận: {request.FullName}, {request.Phone}, tham quan {slotLabel} ngày {date:dd/MM/yyyy}, {request.GuestCount} khách. Trả lời \"đồng ý\" để đặt lịch."
            });
        }

        var booked = await admissions.BookTourAsync(request, InquirySource.Chatbot);
        return AiToolJson.Serialize(new
        {
            booked = true,
            tourId = booked.Id,
            summary = $"Đã đặt lịch tham quan {slotLabel} ngày {booked.VisitDate:dd/MM/yyyy} cho {booked.GuestCount} khách. Lễ tân sẽ gọi xác nhận."
        });
    }
}
