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

namespace NursingHome.Services.Implementations;

/// <summary>S3-H3 (UC-103, UC-104): đặt &amp; duyệt lịch thăm.</summary>
public class VisitService(
    IVisitRepository visits,
    IResidentRepository residents,
    IResidentAccessService access,
    INotificationService notifications,
    IEmailService email,
    IOptions<FacilityOptions> facility,
    FacilityClock clock) : IVisitService
{
    public async Task<List<VisitSlotDto>> GetSlotsAsync(DateOnly date)
    {
        int capacity = facility.Value.VisitSlotCapacity;
        var slots = VisitingHours.Slots(facility.Value.VisitingHours);
        var bookedBySlot = await visits.CountGuestsAsync(date);

        return slots.Select(slot =>
        {
            int booked = bookedBySlot.GetValueOrDefault(slot.Start);
            return new VisitSlotDto
            {
                Start = slot.Start,
                End = slot.End,
                Capacity = capacity,
                Remaining = Math.Max(0, capacity - booked),
                IsPast = clock.UtcNow >= clock.ToUtc(date, slot.Start)
            };
        }).ToList();
    }

    public async Task<VisitBookingDto> BookAsync(CurrentUser user, BookVisitRequest request)
    {
        await access.RequireAsync(user, request.ResidentId, ResidentSection.Basic);
        if (await residents.GetStatusAsync(request.ResidentId) != ResidentStatus.Active)
        {
            throw new BusinessRuleException("Chỉ đặt lịch thăm cho cư dân đang lưu trú.");
        }

        var today = clock.Today;
        if (request.VisitDate < today || request.VisitDate > today.AddDays(30))
        {
            throw new BusinessRuleException("Chỉ đặt lịch thăm trong vòng 30 ngày tới.");
        }

        var slot = VisitingHours.Slots(facility.Value.VisitingHours)
            .FirstOrDefault(s => s.Start == new TimeOnly(request.SlotStart.Hour, request.SlotStart.Minute));
        if (slot == default)
        {
            throw new BusinessRuleException("Khung giờ không nằm trong giờ thăm của cơ sở.");
        }

        if (clock.UtcNow >= clock.ToUtc(request.VisitDate, slot.Start))
        {
            throw new BusinessRuleException("Khung giờ này đã qua hoặc đang diễn ra, vui lòng chọn khung khác.");
        }

        var saved = await visits.BookAsync(new VisitBooking
        {
            ResidentId = request.ResidentId,
            FamilyAccountId = user.AccountId,
            VisitDate = request.VisitDate,
            SlotStart = slot.Start,
            SlotEnd = slot.End,
            GuestCount = request.GuestCount,
            GuestNames = request.GuestNames?.Trim(),
            Note = request.Note?.Trim(),
            Status = VisitStatus.Requested,
            CreatedAt = clock.UtcNow
        }, facility.Value.VisitSlotCapacity);

        await notifications.NotifyRolesAsync(
            [Roles.Receptionist],
            NotificationType.System,
            $"Lịch thăm mới: {saved.Resident.FullName}",
            $"{user.Name} đặt ngày {saved.VisitDate:dd/MM} ({saved.SlotStart:HH\\:mm}–{saved.SlotEnd:HH\\:mm}, {saved.GuestCount} khách).",
            "/staff/visits");

        return saved.ToDto();
    }

    public async Task<List<VisitBookingDto>> ListMineAsync(CurrentUser user) =>
        (await visits.ListForFamilyAsync(user.AccountId)).Select(b => b.ToDto()).ToList();

    public async Task<VisitBookingDto> CancelAsync(CurrentUser user, int id)
    {
        var current = await visits.GetDetailAsync(id) ?? throw new NotFoundException("Không tìm thấy lịch thăm.");
        if (current.FamilyAccountId != user.AccountId)
        {
            throw new ForbiddenException("Bạn không thể hủy lịch thăm của tài khoản khác.");
        }

        if (clock.UtcNow >= clock.ToUtc(current.VisitDate, current.SlotStart))
        {
            throw new BusinessRuleException("Không thể hủy lịch thăm sau khi khung giờ đã bắt đầu.");
        }

        var now = clock.UtcNow;
        await visits.UpdateStatusAsync(id, [VisitStatus.Requested, VisitStatus.Approved], booking =>
        {
            booking.Status = VisitStatus.Cancelled;
            booking.UpdatedAt = now;
        });

        return (await visits.GetDetailAsync(id))!.ToDto();
    }

    public async Task<List<VisitBookingDto>> ListByDateAsync(DateOnly date, string? status)
    {
        VisitStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<VisitStatus>(status, ignoreCase: true, out var s))
            {
                throw new BusinessRuleException("Trạng thái lịch thăm không hợp lệ.");
            }
            parsedStatus = s;
        }

        return (await visits.ListByDateAsync(date, parsedStatus)).Select(b => b.ToDto()).ToList();
    }

    public async Task<VisitBookingDto> ApproveAsync(CurrentUser user, int id, ReviewVisitRequest? request)
    {
        var now = clock.UtcNow;
        await visits.UpdateStatusAsync(id, [VisitStatus.Requested], booking =>
        {
            booking.Status = VisitStatus.Approved;
            booking.ReviewedById = user.AccountId;
            booking.ReviewNote = request?.Note?.Trim();
            booking.UpdatedAt = now;
        });

        var updated = (await visits.GetDetailAsync(id))!;
        await NotifyFamilyStatusAsync(updated, "Đã duyệt", updated.ReviewNote);
        if (email.IsConfigured && !string.IsNullOrWhiteSpace(updated.FamilyAccount.Email))
        {
            await email.SendAsync(
                updated.FamilyAccount.Email,
                $"[An Khang] Xác nhận lịch thăm ngày {updated.VisitDate:dd/MM/yyyy}",
                $"<p>Chào {updated.FamilyAccount.FullName},</p><p>Lịch thăm <b>{updated.Resident.FullName}</b> vào ngày <b>{updated.VisitDate:dd/MM/yyyy}</b> khung <b>{updated.SlotStart:HH\\:mm}–{updated.SlotEnd:HH\\:mm}</b> ({updated.GuestCount} khách) đã được xác nhận.</p>");
        }

        return updated.ToDto();
    }

    public async Task<VisitBookingDto> RejectAsync(CurrentUser user, int id, ReviewVisitRequest request)
    {
        string reason = request.Note?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessRuleException("Khi từ chối lịch thăm cần nhập lý do.");
        }

        var now = clock.UtcNow;
        await visits.UpdateStatusAsync(id, [VisitStatus.Requested], booking =>
        {
            booking.Status = VisitStatus.Rejected;
            booking.ReviewedById = user.AccountId;
            booking.ReviewNote = reason;
            booking.UpdatedAt = now;
        });

        var updated = (await visits.GetDetailAsync(id))!;
        await NotifyFamilyStatusAsync(updated, "Từ chối", reason);
        return updated.ToDto();
    }

    public async Task<VisitBookingDto> CheckInAsync(CurrentUser user, int id)
    {
        var now = clock.UtcNow;
        await visits.UpdateStatusAsync(id, [VisitStatus.Approved], booking =>
        {
            booking.Status = VisitStatus.CheckedIn;
            booking.CheckedInAt = now;
            booking.UpdatedAt = now;
        });

        var updated = (await visits.GetDetailAsync(id))!;
        await NotifyFamilyStatusAsync(updated, "Đã check-in", null);
        return updated.ToDto();
    }

    public async Task<VisitBookingDto> CompleteAsync(CurrentUser user, int id)
    {
        var now = clock.UtcNow;
        await visits.UpdateStatusAsync(id, [VisitStatus.CheckedIn], booking =>
        {
            booking.Status = VisitStatus.Completed;
            booking.UpdatedAt = now;
        });

        var updated = (await visits.GetDetailAsync(id))!;
        await NotifyFamilyStatusAsync(updated, "Đã hoàn tất", null);
        return updated.ToDto();
    }

    public async Task<VisitBookingDto> NoShowAsync(CurrentUser user, int id)
    {
        var now = clock.UtcNow;
        await visits.UpdateStatusAsync(id, [VisitStatus.Approved], booking =>
        {
            booking.Status = VisitStatus.NoShow;
            booking.UpdatedAt = now;
        });

        var updated = (await visits.GetDetailAsync(id))!;
        await NotifyFamilyStatusAsync(updated, "Không đến", null);
        return updated.ToDto();
    }

    private Task NotifyFamilyStatusAsync(VisitBooking booking, string statusLabel, string? note)
    {
        string title = $"Lịch thăm {booking.VisitDate:dd/MM} {booking.SlotStart:HH\\:mm}: {statusLabel}";
        string body = string.IsNullOrWhiteSpace(note)
            ? $"Cư dân: {booking.Resident.FullName} ({booking.GuestCount} khách)."
            : $"Cư dân: {booking.Resident.FullName} — {note}";
        return notifications.NotifyAsync([booking.FamilyAccountId], NotificationType.System, title, body, "/family/visits");
    }
}
