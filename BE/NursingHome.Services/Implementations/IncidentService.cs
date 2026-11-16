using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Care;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>S3-H1 (UC-58, UC-59, UC-107): báo cáo, xử lý sự cố và chia sẻ cho gia đình.</summary>
public class IncidentService(
    IIncidentRepository incidents,
    IResidentRepository residents,
    IResidentAccessService access,
    INotificationService notifications,
    FacilityClock clock) : IIncidentService
{
    public async Task<List<IncidentDto>> ListAsync(CurrentUser user, int? residentId, string? status, string? severity,
        DateOnly? from, DateOnly? to)
    {
        if (residentId.HasValue)
        {
            await access.RequireAsync(user, residentId.Value, ResidentSection.Care);
        }

        var visibleIds = await access.VisibleResidentIdsAsync(user);
        var parsedStatus = ParseOptionalEnum<IncidentStatus>(status, "Trạng thái sự cố không hợp lệ.");
        var parsedSeverity = ParseOptionalEnum<IncidentSeverity>(severity, "Mức độ sự cố không hợp lệ.");
        DateTime? fromUtc = from.HasValue ? clock.StartOfDayUtc(from.Value) : null;
        DateTime? toUtc = to.HasValue ? clock.StartOfDayUtc(to.Value).AddDays(1) : null;

        return (await incidents.ListAsync(residentId, visibleIds, parsedStatus, parsedSeverity, fromUtc, toUtc))
            .Select(item => item.ToDto())
            .ToList();
    }

    public async Task<IncidentDto> GetAsync(CurrentUser user, int id)
    {
        var item = await incidents.GetDetailAsync(id) ?? throw new NotFoundException("Không tìm thấy sự cố.");
        await access.RequireAsync(user, item.ResidentId, ResidentSection.Care);
        return item.ToDto();
    }

    public async Task<IncidentDto> ReportAsync(CurrentUser user, ReportIncidentRequest request)
    {
        await access.RequireAsync(user, request.ResidentId, ResidentSection.Care);
        if (await residents.GetStatusAsync(request.ResidentId) != ResidentStatus.Active)
        {
            throw new BusinessRuleException("Vui lòng chọn cư dân đang lưu trú.");
        }

        if (request.OccurredAt > clock.UtcNow.AddMinutes(5))
        {
            throw new BusinessRuleException("Thời điểm xảy ra không được ở tương lai.");
        }

        if (!Enum.TryParse<IncidentType>(request.Type, ignoreCase: true, out var type))
        {
            throw new BusinessRuleException("Loại sự cố không hợp lệ (Fall, Injury, Behavior, Other).");
        }

        if (!Enum.TryParse<IncidentSeverity>(request.Severity, ignoreCase: true, out var severity))
        {
            throw new BusinessRuleException("Mức độ sự cố không hợp lệ (Low, Medium, High).");
        }

        var occurredAtUtc = request.OccurredAt.Kind == DateTimeKind.Utc
            ? request.OccurredAt
            : request.OccurredAt.ToUniversalTime();

        var saved = await incidents.AddAsync(new Incident
        {
            ResidentId = request.ResidentId,
            Type = type,
            Severity = severity,
            OccurredAt = occurredAtUtc,
            Location = request.Location?.Trim(),
            Description = request.Description.Trim(),
            ImmediateAction = request.ImmediateAction?.Trim(),
            ReportedById = user.AccountId,
            Status = IncidentStatus.Open,
            CreatedAt = clock.UtcNow
        });

        if (saved.Severity == IncidentSeverity.High)
        {
            var resident = await residents.GetByIdAsync(request.ResidentId);
            await notifications.NotifyRolesAsync(
                [Roles.Nurse, Roles.Doctor, Roles.Admin],
                NotificationType.Care,
                $"Sự cố mức CAO: {resident?.FullName}",
                saved.Description,
                $"/staff/incidents?id={saved.Id}");
        }

        return (await incidents.GetDetailAsync(saved.Id))!.ToDto();
    }

    public async Task<IncidentDto> UpdateAsync(CurrentUser user, int id, UpdateIncidentRequest request)
    {
        if (!user.IsInRole(Roles.Admin, Roles.Nurse, Roles.Doctor))
        {
            throw new ForbiddenException("Chỉ Điều dưỡng, Bác sĩ hoặc Admin được cập nhật xử lý sự cố.");
        }

        var current = await incidents.GetDetailAsync(id) ?? throw new NotFoundException("Không tìm thấy sự cố.");
        if (!Enum.TryParse<IncidentStatus>(request.Status, ignoreCase: true, out var nextStatus) ||
            nextStatus is not (IncidentStatus.Investigating or IncidentStatus.Closed))
        {
            throw new BusinessRuleException("Chỉ có thể chuyển trạng thái sang Investigating hoặc Closed.");
        }

        string? resolution = request.Resolution?.Trim();
        if (nextStatus == IncidentStatus.Closed && string.IsNullOrWhiteSpace(resolution))
        {
            throw new BusinessRuleException("Khi đóng sự cố bắt buộc phải nhập kết luận xử lý.");
        }

        if (request.NotifyFamily && (nextStatus != IncidentStatus.Closed || !request.ShareWithFamily))
        {
            throw new BusinessRuleException("Chỉ có thể báo gia đình khi đóng sự cố và chọn chia sẻ với gia đình.");
        }

        byte[] rowVersion;
        try
        {
            rowVersion = Convert.FromBase64String(request.RowVersion);
        }
        catch (FormatException)
        {
            throw new BusinessRuleException("Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại trang.");
        }

        var now = clock.UtcNow;
        await incidents.UpdateStatusAsync(id, rowVersion, item =>
        {
            item.Status = nextStatus;
            item.Resolution = resolution;
            item.ShareWithFamily = request.ShareWithFamily;
            if (nextStatus == IncidentStatus.Closed)
            {
                item.ClosedById = user.AccountId;
                item.ClosedAt = now;
                if (request.NotifyFamily)
                {
                    item.FamilyNotifiedAt = now;
                }
            }
        });

        if (nextStatus == IncidentStatus.Closed && request.NotifyFamily)
        {
            var familyIds = await incidents.FamilyCareViewerIdsAsync(current.ResidentId);
            if (familyIds.Count > 0)
            {
                string typeText = current.Type switch
                {
                    IncidentType.Fall => "Té ngã",
                    IncidentType.Injury => "Chấn thương",
                    IncidentType.Behavior => "Hành vi",
                    _ => "Sự cố khác"
                };
                await notifications.NotifyAsync(
                    familyIds,
                    NotificationType.Care,
                    $"Thông báo sự cố của {current.Resident.FullName}",
                    $"{typeText} — đã xử lý: {resolution}",
                    $"/family/residents/{current.ResidentId}");
            }
        }

        return (await incidents.GetDetailAsync(id))!.ToDto();
    }

    public async Task<List<FamilyIncidentDto>> ListForFamilyAsync(CurrentUser user, int residentId)
    {
        await access.RequireAsync(user, residentId, ResidentSection.Care);
        return (await incidents.ListSharedForResidentAsync(residentId, 50))
            .Select(item => item.ToFamilyDto())
            .ToList();
    }

    private static TEnum? ParseOptionalEnum<TEnum>(string? value, string errorMessage) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed))
        {
            throw new BusinessRuleException(errorMessage);
        }

        return parsed;
    }
}
