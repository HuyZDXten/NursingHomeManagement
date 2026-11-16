using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Billing;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>
/// S3-V5 — UC-85 danh mục dịch vụ phát sinh (Admin), UC-86 ghi lần sử dụng (lễ tân, kế toán, điều dưỡng, hộ lý).
/// Đơn giá chốt lúc ghi; lần sử dụng tự vào hóa đơn tháng (BillingDAO.CreateInvoiceAsync).
/// </summary>
public class ExtraServiceService(
    IExtraServiceRepository services,
    IResidentRepository residents,
    IResidentAccessService access,
    FacilityClock clock) : IExtraServiceService
{
    public const int MaxBackdateDays = 62;
    private const int MaxListDays = 366;

    // ---------------- Danh mục ----------------

    public async Task<List<ExtraServiceDto>> ListServicesAsync(bool activeOnly) =>
        (await services.ListServicesAsync(activeOnly)).Select(item => item.ToDto()).ToList();

    public async Task<ExtraServiceDto> SaveServiceAsync(int? id, SaveExtraServiceRequest request)
    {
        if (decimal.Round(request.UnitPrice, 0) != request.UnitPrice || request.UnitPrice < 0)
        {
            throw new BusinessRuleException("Đơn giá phải là số đồng nguyên, không âm.");
        }

        var name = request.Name.Trim();
        if (await services.ServiceNameExistsAsync(name, id))
        {
            throw new ConflictException("Tên dịch vụ đã tồn tại.");
        }

        if (id.HasValue)
        {
            var existing = await services.GetServiceAsync(id.Value) ?? throw new NotFoundException("Không tìm thấy dịch vụ.");
            existing.Name = name;
            existing.Unit = request.Unit.Trim();
            existing.UnitPrice = request.UnitPrice;
            existing.IsActive = request.IsActive;
            existing.UpdatedAt = clock.UtcNow;
            await services.UpdateServiceAsync(existing);
            return existing.ToDto();
        }

        var created = await services.AddServiceAsync(new ExtraService
        {
            Name = name,
            Unit = request.Unit.Trim(),
            UnitPrice = request.UnitPrice,
            IsActive = request.IsActive,
            CreatedAt = clock.UtcNow
        });
        return created.ToDto();
    }

    // ---------------- Lần sử dụng ----------------

    public async Task<List<ServiceUsageDto>> ListUsagesAsync(CurrentUser user, int? residentId, DateOnly? from, DateOnly? to)
    {
        var end = to ?? clock.Today;
        var start = from ?? new DateOnly(end.Year, end.Month, 1);
        if (end < start || end.DayNumber - start.DayNumber >= MaxListDays)
        {
            throw new BusinessRuleException($"Khoảng ngày không hợp lệ (tối đa {MaxListDays} ngày).");
        }

        IReadOnlyCollection<int>? scope;
        if (residentId is int id)
        {
            await access.RequireAsync(user, id, ResidentSection.Basic);
            scope = [id];
        }
        else
        {
            scope = await access.VisibleResidentIdsAsync(user);
        }

        return (await services.ListUsagesAsync(scope, start, end, take: 500)).Select(item => item.ToDto()).ToList();
    }

    public async Task<ServiceUsageDto> RecordUsageAsync(CurrentUser user, int residentId, CreateServiceUsageRequest request)
    {
        await access.RequireAsync(user, residentId, ResidentSection.Basic);
        if (await residents.GetStatusAsync(residentId) != ResidentStatus.Active)
        {
            throw new BusinessRuleException("Chỉ ghi dịch vụ cho cư dân đang lưu trú.");
        }

        if (ValidateUsage(request.Quantity, request.UsedOn ?? clock.Today, clock.Today) is { } error)
        {
            throw new BusinessRuleException(error);
        }

        var service = await services.GetServiceAsync(request.ExtraServiceId);
        if (service is null || !service.IsActive)
        {
            throw new BusinessRuleException("Dịch vụ không tồn tại hoặc đã ngừng cung cấp.");
        }

        var created = await services.AddUsageAsync(new ServiceUsage
        {
            ResidentId = residentId,
            ExtraServiceId = service.Id,
            UsedOn = request.UsedOn ?? clock.Today,
            Quantity = request.Quantity,
            UnitPrice = service.UnitPrice,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            RecordedById = user.AccountId,
            CreatedAt = clock.UtcNow
        });
        return (await services.GetUsageAsync(created.Id) ?? throw new NotFoundException("Không tìm thấy lần sử dụng.")).ToDto();
    }

    /// <summary>Điều dưỡng/hộ lý chỉ xóa lần mình ghi; lễ tân, kế toán, Admin xóa được mọi lần chưa vào hóa đơn.</summary>
    public async Task DeleteUsageAsync(CurrentUser user, int id)
    {
        var usage = await services.GetUsageAsync(id) ?? throw new NotFoundException("Không tìm thấy lần sử dụng.");
        if (user.IsInRole(Roles.Nurse, Roles.Caregiver) && usage.RecordedById != user.AccountId)
        {
            throw new ForbiddenException("Chỉ xóa được lần sử dụng do chính bạn ghi.");
        }

        await services.DeleteUsageAsync(id);
    }

    /// <summary>Số lượng tối đa 2 chữ số thập phân, ngày dùng không ở tương lai và không quá <see cref="MaxBackdateDays"/> ngày trước.</summary>
    public static string? ValidateUsage(decimal quantity, DateOnly usedOn, DateOnly today)
    {
        if (quantity <= 0 || decimal.Round(quantity, 2) != quantity)
        {
            return "Số lượng phải lớn hơn 0 và có tối đa 2 chữ số thập phân.";
        }

        if (usedOn > today)
        {
            return "Ngày sử dụng không được ở tương lai.";
        }

        return today.DayNumber - usedOn.DayNumber > MaxBackdateDays ? $"Chỉ ghi bổ sung trong vòng {MaxBackdateDays} ngày." : null;
    }
}
