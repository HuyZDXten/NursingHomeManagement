using System.Globalization;
using System.Text.Json;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Common;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Ai.Tools;

// ======================================================================================================
// S4-H1 — AI-02: Tool trợ lý AI dành cho gia đình.
// Luôn gọi qua Service với CurrentUser của người hỏi (KHÔNG gọi DAO/Repository trực tiếp) để giữ phân quyền.
// ======================================================================================================

public static class FamilyTools
{
    public static IReadOnlyList<IAiTool> Create(
        IFamilyService family,
        IFamilyTimelineService timeline,
        IHealthService health,
        IVisitService visits,
        FacilityClock clock) =>
    [
        new ListMyResidentsTool(family),
        new GetDaySummaryTool(timeline, clock),
        new GetRecentVitalsTool(health, clock),
        new GetActiveMedicationsTool(family),
        new GetOpenInvoicesTool(family),
        new GetMyVisitsTool(visits, clock)
    ];

    internal static async Task<string> GuardAsync(Func<Task<string>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is ForbiddenException or NotFoundException or BusinessRuleException)
        {
            return AiToolJson.Serialize(new { error = ex.Message, summary = ex.Message });
        }
    }

    internal static CurrentUser RequireUser(CurrentUser? user) =>
        user ?? throw new ForbiddenException("Bạn cần đăng nhập tài khoản gia đình.");
}

public sealed class ListMyResidentsTool(IFamilyService family) : IAiTool
{
    public AiToolDefinition Definition { get; } = new(
        "list_my_residents",
        "Danh sách người thân đang liên kết với tài khoản gia đình hiện tại kèm quyền được phép xem.",
        AiToolJson.Schema());

    public Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default) =>
        FamilyTools.GuardAsync(async () =>
        {
            var list = await family.MyResidentsAsync(FamilyTools.RequireUser(user));
            return AiToolJson.Serialize(new
            {
                count = list.Count,
                residents = list.Select(r => new
                {
                    r.ResidentId,
                    r.Code,
                    r.FullName,
                    r.CanViewHealth,
                    r.CanViewCare,
                    r.CanViewMedication,
                    r.CanViewBilling
                }),
                summary = list.Count == 0
                    ? "Tài khoản chưa liên kết với cư dân nào."
                    : "Người thân của bạn: " + string.Join("; ", list.Select(r => $"{r.FullName} (mã {r.Code}, id={r.ResidentId})")) + "."
            });
        });
}

public sealed class GetDaySummaryTool(IFamilyTimelineService timeline, FacilityClock clock) : IAiTool
{
    public AiToolDefinition Definition { get; } = new(
        "get_day_summary",
        "Nhật ký sinh hoạt, ăn uống, chăm sóc, sinh hiệu đã chia sẻ trong một ngày của cư dân.",
        AiToolJson.Schema(
            ("residentId", "integer", "Id cư dân", true),
            ("date", "string", "Ngày dạng yyyy-MM-dd (mặc định hôm nay)", false)));

    public Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default) =>
        FamilyTools.GuardAsync(async () =>
        {
            int residentId = AiToolJson.Int(arguments, "residentId") ?? 0;
            if (residentId <= 0)
            {
                return AiToolJson.Serialize(new { error = "Cần residentId hợp lệ.", summary = "Anh/chị muốn hỏi về người thân nào ạ?" });
            }

            string? dateStr = AiToolJson.String(arguments, "date");
            DateOnly date = DateOnly.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : clock.Today;

            var items = await timeline.GetForDateAsync(FamilyTools.RequireUser(user), residentId, date);
            return AiToolJson.Serialize(new
            {
                residentId,
                date = date.ToString("yyyy-MM-dd"),
                items = items.Select(i => new { i.Kind, at = i.At, i.Title, i.Detail, i.RecordedBy }),
                summary = items.Count == 0
                    ? $"Ngày {date:dd/MM/yyyy} chưa có ghi nhận nào được chia sẻ."
                    : $"Ngày {date:dd/MM/yyyy}: " + string.Join("; ", items.Select(i => i.Detail)) + "."
            });
        });
}

public sealed class GetRecentVitalsTool(IHealthService health, FacilityClock clock) : IAiTool
{
    public AiToolDefinition Definition { get; } = new(
        "get_recent_vitals",
        "Các chỉ số sinh hiệu gần đây đã chia sẻ với gia đình (huyết áp, mạch, nhiệt độ, SpO2).",
        AiToolJson.Schema(
            ("residentId", "integer", "Id cư dân", true),
            ("days", "integer", "Số ngày gần nhất (1–14, mặc định 7)", false)));

    public Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default) =>
        FamilyTools.GuardAsync(async () =>
        {
            int residentId = AiToolJson.Int(arguments, "residentId") ?? 0;
            if (residentId <= 0)
            {
                return AiToolJson.Serialize(new { error = "Cần residentId hợp lệ.", summary = "Anh/chị muốn xem sinh hiệu của người thân nào ạ?" });
            }

            int days = Math.Clamp(AiToolJson.Int(arguments, "days") ?? 7, 1, 14);
            var today = clock.Today;
            var list = await health.ListAsync(FamilyTools.RequireUser(user), residentId, today.AddDays(-days), today);
            return AiToolJson.Serialize(new
            {
                residentId,
                days,
                vitals = list.Take(10).Select(v => new { v.RecordedAt, v.Systolic, v.Diastolic, v.HeartRate, v.Temperature, v.SpO2 }),
                summary = list.Count == 0
                    ? $"Trong {days} ngày qua chưa có chỉ số sinh hiệu nào được chia sẻ."
                    : $"Lần đo gần nhất ({list[0].RecordedAt + FacilityClock.Offset:dd/MM HH\\:mm}): HA {list[0].Systolic}/{list[0].Diastolic}, mạch {list[0].HeartRate}, {list[0].Temperature}°C."
            });
        });
}

public sealed class GetActiveMedicationsTool(IFamilyService family) : IAiTool
{
    public AiToolDefinition Definition { get; } = new(
        "get_active_medications",
        "Danh sách thuốc cư dân đang dùng (nếu tài khoản được cấp quyền xem thuốc).",
        AiToolJson.Schema(("residentId", "integer", "Id cư dân", true)));

    public Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default) =>
        FamilyTools.GuardAsync(async () =>
        {
            int residentId = AiToolJson.Int(arguments, "residentId") ?? 0;
            if (residentId <= 0)
            {
                return AiToolJson.Serialize(new { error = "Cần residentId hợp lệ.", summary = "Anh/chị muốn hỏi lịch thuốc của người thân nào ạ?" });
            }

            var detail = await family.DetailsAsync(FamilyTools.RequireUser(user), residentId);
            if (!detail.Resident.CanViewMedication)
            {
                return AiToolJson.Serialize(new
                {
                    error = "Tài khoản không có quyền xem thông tin thuốc của cư dân này.",
                    summary = "Tài khoản của bạn chưa được cấp quyền xem thông tin thuốc của cư dân này."
                });
            }

            var meds = detail.Medications.Where(m => m.IsActive).ToList();
            return AiToolJson.Serialize(new
            {
                residentId,
                medications = meds.Select(m => new { m.MedicationName, m.ScheduledTime }),
                summary = meds.Count == 0
                    ? "Hiện chưa có lịch thuốc đang dùng nào."
                    : "Thuốc đang dùng: " + string.Join("; ", meds.Select(m => $"{m.MedicationName} ({m.ScheduledTime:HH\\:mm})")) + "."
            });
        });
}

public sealed class GetOpenInvoicesTool(IFamilyService family) : IAiTool
{
    public AiToolDefinition Definition { get; } = new(
        "get_open_invoices",
        "Các hóa đơn còn công nợ của cư dân (nếu tài khoản được cấp quyền xem hóa đơn).",
        AiToolJson.Schema(("residentId", "integer", "Id cư dân", true)));

    public Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default) =>
        FamilyTools.GuardAsync(async () =>
        {
            int residentId = AiToolJson.Int(arguments, "residentId") ?? 0;
            if (residentId <= 0)
            {
                return AiToolJson.Serialize(new { error = "Cần residentId hợp lệ.", summary = "Anh/chị muốn kiểm tra hóa đơn của người thân nào ạ?" });
            }

            var detail = await family.DetailsAsync(FamilyTools.RequireUser(user), residentId);
            if (!detail.Resident.CanViewBilling)
            {
                return AiToolJson.Serialize(new
                {
                    error = "Tài khoản không có quyền xem hóa đơn của cư dân này.",
                    summary = "Tài khoản của bạn chưa được cấp quyền xem hóa đơn của cư dân này."
                });
            }

            var open = detail.Invoices.Where(i => i.Remaining > 0 && i.Status != "Cancelled").ToList();
            return AiToolJson.Serialize(new
            {
                residentId,
                openInvoices = open.Select(i => new { i.Number, i.BillingPeriod, i.Remaining, i.DueDate }),
                summary = open.Count == 0
                    ? "Hiện tại không có hóa đơn nào còn nợ."
                    : "Hóa đơn còn nợ: " + string.Join("; ", open.Select(i => $"{i.Number} (còn {PublicTools.Money(i.Remaining)}, hạn {i.DueDate:dd/MM/yyyy})")) + "."
            });
        });
}

public sealed class GetMyVisitsTool(IVisitService visits, FacilityClock clock) : IAiTool
{
    public AiToolDefinition Definition { get; } = new(
        "get_my_visits",
        "Danh sách lịch thăm người thân sắp tới của tài khoản gia đình đang đăng nhập.",
        AiToolJson.Schema());

    public Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default) =>
        FamilyTools.GuardAsync(async () =>
        {
            var today = clock.Today;
            var all = await visits.ListMineAsync(FamilyTools.RequireUser(user));
            var upcoming = all
                .Where(v => v.VisitDate >= today && v.Status is "Requested" or "Approved" or "CheckedIn")
                .OrderBy(v => v.VisitDate)
                .ThenBy(v => v.SlotStart)
                .ToList();

            return AiToolJson.Serialize(new
            {
                count = upcoming.Count,
                visits = upcoming.Select(v => new { v.ResidentName, v.VisitDate, v.SlotStart, v.SlotEnd, v.GuestCount, v.Status }),
                summary = upcoming.Count == 0
                    ? "Hiện chưa có lịch thăm sắp tới nào."
                    : "Lịch thăm sắp tới: " + string.Join("; ", upcoming.Select(v => $"{v.ResidentName} ngày {v.VisitDate:dd/MM} ({v.SlotStart:HH\\:mm}–{v.SlotEnd:HH\\:mm}, {v.Status})")) + "."
            });
        });
}
