using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Care;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.Services.Ai;

/// <summary>S4-H2 — AI-03: soạn bản nháp nhật ký hằng ngày cho gia đình; chỉ bản Approved mới gửi cho gia đình.</summary>
public sealed class DailyDigestService(
    IAiInsightRepository insights,
    IFamilyCareRepository familyCare,
    IIncidentRepository incidents,
    IResidentAccessService access,
    INotificationService notifications,
    IAiChatProvider provider,
    IOptions<AiOptions> options,
    IOptions<FacilityOptions> facility,
    FacilityClock clock,
    ILogger<DailyDigestService> logger) : IDailyDigestService
{
    private const string SystemPrompt = """
        Bạn là điều dưỡng của viện dưỡng lão viết bản tin cuối ngày gửi người nhà cư dân bằng tiếng Việt ấm áp, 4–6 câu.
        Chỉ nêu các sự kiện có trong dữ liệu được cung cấp (bữa ăn, sinh hoạt, chăm sóc, sinh hiệu đã chia sẻ).
        Không tự bịa thêm sự kiện, không nhận định hay chẩn đoán y tế, không nhắc SĐT/CCCD/địa chỉ.
        """;

    public async Task<int> GenerateForDateAsync(DateOnly forDate, CancellationToken cancellationToken = default)
    {
        int created = 0;
        var startUtc = clock.StartOfDayUtc(forDate);
        var endUtc = startUtc.AddDays(1);
        var targets = await insights.ActiveResidentsWithFamilyAsync();

        foreach (var (resident, includeCare, includeHealth) in targets)
        {
            if (!includeCare && !includeHealth)
            {
                continue;
            }

            var entries = await familyCare.TimelineForDateAsync(resident.Id, forDate, startUtc, endUtc, includeCare, includeHealth);
            if (entries.Count == 0)
            {
                continue;
            }

            var sourceJson = AiToolJson.Serialize(entries.Select(e => new
            {
                e.Kind,
                Time = (e.At + FacilityClock.Offset).ToString("HH:mm"),
                e.Title,
                e.Detail
            }));

            string content = await DraftContentAsync(resident.FullName, forDate, sourceJson, entries, cancellationToken);
            bool added = await insights.TryAddAsync(new AiInsight
            {
                ResidentId = resident.Id,
                Type = InsightType.DailyDigest,
                ForDate = forDate,
                Content = content,
                SourceJson = sourceJson,
                Status = InsightStatus.Draft,
                GeneratedAt = clock.UtcNow
            });
            if (added)
            {
                created++;
            }
        }

        return created;
    }

    public async Task<List<AiInsightDto>> ListAsync(DateOnly? date, string? type, string? status)
    {
        InsightType? parsedType = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<InsightType>(type, ignoreCase: true, out var t))
            {
                throw new BusinessRuleException("Loại bản tin AI không hợp lệ (DailyDigest hoặc VitalTrend).");
            }
            parsedType = t;
        }

        InsightStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<InsightStatus>(status, ignoreCase: true, out var s))
            {
                throw new BusinessRuleException("Trạng thái không hợp lệ (Draft, Approved hoặc Rejected).");
            }
            parsedStatus = s;
        }

        return (await insights.ListAsync(date, parsedType, parsedStatus)).Select(i => i.ToDto()).ToList();
    }

    public async Task<AiInsightDto> ApproveAsync(CurrentUser user, long id, ApproveInsightRequest request)
    {
        var insight = await insights.GetDetailAsync(id) ?? throw new NotFoundException("Không tìm thấy bản nháp.");
        if (insight.Type != InsightType.DailyDigest)
        {
            throw new BusinessRuleException("Chỉ duyệt gửi gia đình cho bản tin DailyDigest.");
        }

        if (insight.Status != InsightStatus.Draft)
        {
            throw new BusinessRuleException("Bản nháp này đã được xử lý trước đó.");
        }

        insight.Content = request.Content.Trim();
        insight.Status = InsightStatus.Approved;
        insight.ReviewedById = user.AccountId;
        insight.ReviewedAt = clock.UtcNow;
        await insights.UpdateAsync(insight);

        var familyIds = await incidents.FamilyCareViewerIdsAsync(insight.ResidentId);
        if (familyIds.Count > 0)
        {
            await notifications.NotifyAsync(
                familyIds,
                NotificationType.Care,
                $"Nhật ký hôm nay của {insight.Resident.FullName}",
                insight.Content,
                $"/family/residents/{insight.ResidentId}");
        }

        return (await insights.GetDetailAsync(id))!.ToDto();
    }

    public async Task<AiInsightDto> RejectAsync(CurrentUser user, long id)
    {
        var insight = await insights.GetDetailAsync(id) ?? throw new NotFoundException("Không tìm thấy bản nháp.");
        if (insight.Status != InsightStatus.Draft)
        {
            throw new BusinessRuleException("Bản nháp này đã được xử lý trước đó.");
        }

        insight.Status = InsightStatus.Rejected;
        insight.ReviewedById = user.AccountId;
        insight.ReviewedAt = clock.UtcNow;
        await insights.UpdateAsync(insight);
        return (await insights.GetDetailAsync(id))!.ToDto();
    }

    public async Task<List<AiInsightDto>> ListForFamilyAsync(CurrentUser user, int residentId, DateOnly? date)
    {
        await access.RequireAsync(user, residentId, ResidentSection.Care);
        return (await insights.ListApprovedDigestsAsync(residentId, date)).Select(i => i.ToDto()).ToList();
    }

    private async Task<string> DraftContentAsync(
        string residentName,
        DateOnly forDate,
        string sourceJson,
        IReadOnlyList<DataAccessObjects.TimelineEntry> entries,
        CancellationToken cancellationToken)
    {
        if (options.Value.Enabled && provider.Name != AiOptions.FakeProvider)
        {
            try
            {
                var result = await provider.CompleteAsync(new AiChatRequest(
                    SystemPrompt,
                    [new AiMessage("user", $"Cư dân: {residentName}, ngày {forDate:dd/MM/yyyy} tại {facility.Value.Name}. Dữ liệu:\n{sourceJson}")],
                    [],
                    Temperature: 0.2), cancellationToken);
                if (!string.IsNullOrWhiteSpace(result.Text))
                {
                    return result.Text.Trim();
                }
            }
            catch (Exception ex) when (ex is AiProviderException or HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("AI lỗi khi soạn nhật ký cho {Resident}, dùng bản tóm tắt theo quy tắc", residentName);
            }
        }

        return $"Ngày {forDate:dd/MM/yyyy}, tại {facility.Value.Name}, tình hình sinh hoạt của cụ {residentName} được ghi nhận như sau: "
            + string.Join("; ", entries.Select(e => e.Detail)) + ".";
    }
}

/// <summary>S4-H2 — AI-06: tính xu hướng sinh hiệu 14 ngày bằng C# và gọi AI diễn giải khi bất thường.</summary>
public sealed class VitalTrendService(
    IAiInsightRepository insights,
    IClinicalRepository clinical,
    IResidentAccessService access,
    INotificationService notifications,
    IAiChatProvider provider,
    IOptions<AiOptions> options,
    IOptions<FacilityOptions> facility,
    FacilityClock clock,
    ILogger<VitalTrendService> logger) : IVitalTrendService
{
    private const string SystemPrompt = """
        Bạn là trợ lý lâm sàng tóm tắt xu hướng sinh hiệu cho điều dưỡng và bác sĩ bằng tiếng Việt.
        Chỉ diễn giải ngắn gọn 1–2 câu dựa đúng trên các bất thường thống kê đã tính sẵn dưới đây.
        Không chẩn đoán bệnh, không đề xuất thuốc hay liều.
        """;

    public async Task<int> AnalyzeForDateAsync(DateOnly forDate, CancellationToken cancellationToken = default)
    {
        int created = 0;
        var sinceUtc = clock.StartOfDayUtc(forDate.AddDays(-13));
        var residents = await insights.ActiveResidentsAsync();

        foreach (var resident in residents)
        {
            var vitals = await insights.Last14DaysVitalsAsync(resident.Id, sinceUtc);
            if (vitals.Count == 0)
            {
                continue;
            }

            var custom = await clinical.GetThresholdAsync(resident.Id);
            var limits = custom is null
                ? facility.Value.VitalAlerts
                : new VitalAlertOptions
                {
                    SystolicHigh = custom.SystolicHigh,
                    SystolicLow = custom.SystolicLow,
                    HeartRateHigh = custom.HeartRateHigh,
                    HeartRateLow = custom.HeartRateLow,
                    TemperatureHigh = custom.TemperatureHigh,
                    SpO2Low = custom.SpO2Low
                };

            var findings = VitalTrends.Analyze(vitals, limits);
            if (findings.Count == 0)
            {
                continue;
            }

            var sourceJson = AiToolJson.Serialize(findings);
            string content = await ExplainAsync(resident.FullName, sourceJson, findings, cancellationToken);

            bool added = await insights.TryAddAsync(new AiInsight
            {
                ResidentId = resident.Id,
                Type = InsightType.VitalTrend,
                ForDate = forDate,
                Content = content,
                SourceJson = sourceJson,
                Status = InsightStatus.Draft,
                GeneratedAt = clock.UtcNow
            });

            if (added)
            {
                created++;
                await notifications.NotifyRolesAsync(
                    [Roles.Nurse, Roles.Doctor],
                    NotificationType.Health,
                    $"Xu hướng sinh hiệu cần xem lại: {resident.FullName}",
                    content,
                    $"/staff/residents/{resident.Id}");
            }
        }

        return created;
    }

    public async Task<List<AiInsightDto>> ListForResidentAsync(CurrentUser user, int residentId)
    {
        await access.RequireAsync(user, residentId, ResidentSection.Health);
        return (await insights.ListVitalTrendsAsync(residentId, 10)).Select(i => i.ToDto()).ToList();
    }

    private async Task<string> ExplainAsync(
        string residentName,
        string sourceJson,
        IReadOnlyList<TrendFinding> findings,
        CancellationToken cancellationToken)
    {
        if (options.Value.Enabled && provider.Name != AiOptions.FakeProvider)
        {
            try
            {
                var result = await provider.CompleteAsync(new AiChatRequest(
                    SystemPrompt,
                    [new AiMessage("user", $"Cư dân: {residentName}. Phát hiện thống kê 14 ngày:\n{sourceJson}")],
                    [],
                    Temperature: 0.2), cancellationToken);
                if (!string.IsNullOrWhiteSpace(result.Text))
                {
                    return result.Text.Trim();
                }
            }
            catch (Exception ex) when (ex is AiProviderException or HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("AI lỗi khi diễn giải xu hướng sinh hiệu cho {Resident}", residentName);
            }
        }

        return string.Join(" ", findings.Select(f => f.Detail));
    }
}
