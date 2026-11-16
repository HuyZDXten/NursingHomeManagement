using NursingHome.BusinessObjects.Entities;
using NursingHome.DTOs.Rooms;

namespace NursingHome.Services.Helpers;

public static partial class OperationsMapper
{
    public static RoomTypeDto ToDto(this RoomType type) => new()
    {
        Id = type.Id,
        Name = type.Name,
        DailySurcharge = type.DailySurcharge,
        Description = type.Description,
        IsActive = type.IsActive
    };

    /// <summary>Active nhưng đã quá hạn hiển thị là "Expired" (chưa ai đóng lại trong DB).</summary>
    public static BedReservationDto ToDto(this BedReservation reservation, DateTime now) => new()
    {
        Id = reservation.Id,
        BedId = reservation.BedId,
        BedLabel = reservation.Bed is null ? "" : $"Phòng {reservation.Bed.Room?.Number} - Giường {reservation.Bed.Number}",
        InquiryId = reservation.InquiryId,
        HolderName = reservation.HolderName,
        Phone = reservation.Phone,
        Note = reservation.Note,
        Status = reservation.Status == ReservationStatus.Active && reservation.ExpiresAt <= now ? "Expired" : reservation.Status.ToString(),
        ReservedByName = reservation.ReservedBy?.FullName,
        ReservedAt = reservation.ReservedAt,
        ExpiresAt = reservation.ExpiresAt,
        ClosedAt = reservation.ClosedAt
    };

    public static MaintenanceRequestDto ToDto(this MaintenanceRequest request) => new()
    {
        Id = request.Id,
        RoomId = request.RoomId,
        RoomNumber = request.Room?.Number ?? "",
        BedId = request.BedId,
        BedNumber = request.Bed?.Number,
        Title = request.Title,
        Description = request.Description,
        Priority = request.Priority.ToString(),
        Status = request.Status.ToString(),
        BlocksBed = request.BlocksBed,
        ReportedByName = request.ReportedBy?.FullName,
        ReportedAt = request.ReportedAt,
        AssignedTo = request.AssignedTo,
        ResolvedAt = request.ResolvedAt,
        ResolutionNote = request.ResolutionNote
    };
}
