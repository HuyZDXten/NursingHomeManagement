using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Common;
using NursingHome.DTOs.Rooms;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>
/// BL-V3: loại phòng & phụ thu (UC-36; phụ thu cộng vào hóa đơn ở BillingService), giữ giường (UC-41; chặn xếp người
/// khác ở RoomDAO/ResidentRecordsDAO qua BedReservationGuard), báo hỏng & sửa chữa (UC-42).
/// </summary>
public class RoomOpsService(IRoomOpsRepository roomOps, INotificationService notifications, FacilityClock clock) : IRoomOpsService
{
    private const string MaintenanceLink = "/staff/room-ops?tab=maintenance";

    // ---------------- Loại phòng ----------------

    public async Task<List<RoomTypeDto>> ListTypesAsync(bool activeOnly) =>
        (await roomOps.ListTypesAsync(activeOnly)).Select(item => item.ToDto()).ToList();

    public async Task<RoomTypeDto> SaveTypeAsync(int? id, SaveRoomTypeRequest request)
    {
        string name = request.Name.Trim();
        RoomRules.EnsureWholeVnd(request.DailySurcharge);
        if (await roomOps.TypeNameExistsAsync(name, id))
        {
            throw new ConflictException("Tên loại phòng đã tồn tại.");
        }

        if (id is { } existingId)
        {
            var type = await roomOps.GetTypeAsync(existingId) ?? throw new NotFoundException("Không tìm thấy loại phòng.");
            type.Name = name;
            type.DailySurcharge = request.DailySurcharge;
            type.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            type.IsActive = request.IsActive;
            type.UpdatedAt = clock.UtcNow;
            await roomOps.UpdateTypeAsync(type);
            return type.ToDto();
        }

        return (await roomOps.AddTypeAsync(new RoomType
        {
            Name = name,
            DailySurcharge = request.DailySurcharge,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = request.IsActive,
            CreatedAt = clock.UtcNow
        })).ToDto();
    }

    // ---------------- Giữ giường ----------------

    public async Task<List<BedReservationDto>> ListReservationsAsync(bool activeOnly)
    {
        var now = clock.UtcNow;
        return (await roomOps.ListReservationsAsync(activeOnly, now)).Select(item => item.ToDto(now)).ToList();
    }

    public async Task<BedReservationDto> CreateReservationAsync(CurrentUser user, CreateBedReservationRequest request)
    {
        var now = clock.UtcNow;
        var expiresAt = RoomRules.ReservationExpiry(request.ExpiresOn, clock.Today, clock);
        var created = await roomOps.CreateReservationAsync(new BedReservation
        {
            BedId = request.BedId,
            InquiryId = request.InquiryId,
            HolderName = request.HolderName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Status = ReservationStatus.Active,
            ReservedById = user.AccountId,
            ReservedAt = now,
            ExpiresAt = expiresAt
        }, now);
        return (await roomOps.GetReservationAsync(created.Id))!.ToDto(now);
    }

    public async Task<BedReservationDto> ReleaseReservationAsync(CurrentUser user, int id)
    {
        _ = await roomOps.GetReservationAsync(id) ?? throw new NotFoundException("Không tìm thấy lượt giữ giường.");
        if (!await roomOps.ReleaseReservationAsync(id, user.AccountId, clock.UtcNow))
        {
            throw new BusinessRuleException("Lượt giữ giường đã kết thúc.");
        }

        return (await roomOps.GetReservationAsync(id))!.ToDto(clock.UtcNow);
    }

    // ---------------- Báo hỏng & sửa chữa ----------------

    public async Task<List<MaintenanceRequestDto>> ListMaintenanceAsync(string? status, int? roomId)
    {
        MaintenanceStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            filter = Enum.TryParse<MaintenanceStatus>(status, out var parsed) ? parsed : throw new BusinessRuleException("Trạng thái không hợp lệ.");
        }

        return (await roomOps.ListMaintenanceAsync(filter, roomId)).Select(item => item.ToDto()).ToList();
    }

    public async Task<MaintenanceRequestDto> CreateMaintenanceAsync(CurrentUser user, CreateMaintenanceRequest request)
    {
        if (request.BlocksBed && request.BedId is null)
        {
            throw new BusinessRuleException("Chọn giường khi muốn khóa giường để sửa.");
        }

        var created = await roomOps.CreateMaintenanceAsync(new MaintenanceRequest
        {
            RoomId = request.RoomId,
            BedId = request.BedId,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Priority = Enum.Parse<MaintenancePriority>(request.Priority),
            Status = MaintenanceStatus.Open,
            BlocksBed = request.BlocksBed,
            ReportedById = user.AccountId,
            ReportedAt = clock.UtcNow
        }, clock.UtcNow);
        var dto = (await roomOps.GetMaintenanceAsync(created.Id))!.ToDto();
        if (created.Priority == MaintenancePriority.High)
        {
            await notifications.NotifyRolesAsync([Roles.Admin], NotificationType.System,
                $"Báo hỏng khẩn: phòng {dto.RoomNumber}", dto.Title, MaintenanceLink);
        }

        return dto;
    }

    public async Task<MaintenanceRequestDto> UpdateMaintenanceAsync(CurrentUser user, int id, UpdateMaintenanceRequest request)
    {
        var before = await roomOps.GetMaintenanceAsync(id) ?? throw new NotFoundException("Không tìm thấy phiếu sửa chữa.");
        var status = Enum.Parse<MaintenanceStatus>(request.Status);
        await roomOps.UpdateMaintenanceAsync(id, status,
            string.IsNullOrWhiteSpace(request.AssignedTo) ? null : request.AssignedTo.Trim(),
            string.IsNullOrWhiteSpace(request.ResolutionNote) ? null : request.ResolutionNote.Trim(),
            user.AccountId, clock.UtcNow);
        if (status == MaintenanceStatus.Done && before.ReportedById != user.AccountId)
        {
            await notifications.NotifyAsync([before.ReportedById], NotificationType.System,
                $"Đã sửa xong: {before.Title}", $"Phòng {before.Room?.Number}", MaintenanceLink);
        }

        return (await roomOps.GetMaintenanceAsync(id))!.ToDto();
    }
}
