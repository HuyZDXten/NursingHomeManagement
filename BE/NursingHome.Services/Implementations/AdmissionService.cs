using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Admissions;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.Services.Implementations;

/// <summary>
/// S3-V1 — UC-17 tư vấn, UC-18 đặt lịch tham quan, UC-21 phễu khách, UC-22 xác nhận lịch.
/// Phễu: New → Contacted → Visited → Admitted; Lost được từ mọi trạng thái chưa kết thúc; Admitted/Lost là cuối.
/// </summary>
public partial class AdmissionService(
    IAdmissionRepository admissions,
    IAccountRepository accounts,
    IResidentRepository residents,
    IBillingRepository billing,
    INotificationService notifications,
    IEmailService email,
    IOptions<FacilityOptions> facility,
    FacilityClock clock,
    ILogger<AdmissionService> logger) : IAdmissionService
{
    /// <summary>Đặt lịch từ ngày mai đến tối đa 60 ngày tới.</summary>
    public const int MaxDaysAhead = 60;

    private const string AdmissionsLink = "/staff/admissions";

    private static readonly string[] AssignableRoles = [Roles.Receptionist, Roles.Admin];
    private static readonly TourSlot[] Slots = [TourSlot.Morning, TourSlot.Afternoon];

    // ---------------- Công khai ----------------

    public async Task<InquiryDto> CreateInquiryAsync(CreateInquiryRequest request, InquirySource source)
    {
        if (request.InterestedPackageId is int packageId)
        {
            var package = await billing.GetPackageAsync(packageId);
            if (package is null || !package.IsActive)
            {
                throw new BusinessRuleException("Gói dịch vụ không tồn tại hoặc đã ngừng cung cấp.");
            }
        }

        var inquiry = await admissions.AddInquiryAsync(new Inquiry
        {
            FullName = request.FullName.Trim(),
            Phone = NormalizePhone(request.Phone),
            Email = Clean(request.Email),
            ElderName = Clean(request.ElderName),
            ElderAge = request.ElderAge,
            CareNeeds = Clean(request.CareNeeds),
            InterestedPackageId = request.InterestedPackageId,
            Source = source,
            Status = InquiryStatus.New,
            CreatedAt = clock.UtcNow
        });

        await notifications.NotifyRolesAsync([Roles.Receptionist], NotificationType.System,
            "Yêu cầu tư vấn mới", $"{inquiry.FullName} — {inquiry.Phone}", AdmissionsLink);
        return await GetInquiryDtoAsync(inquiry.Id);
    }

    public async Task<TourBookingDto> BookTourAsync(BookTourRequest request, InquirySource source = InquirySource.Web)
    {
        var slot = ParseEnum<TourSlot>(request.TimeSlot, "Buổi tham quan phải là Morning (sáng) hoặc Afternoon (chiều).");
        var today = clock.Today;
        if (request.VisitDate <= today || request.VisitDate > today.AddDays(MaxDaysAhead))
        {
            throw new BusinessRuleException(
                $"Chỉ nhận đặt lịch từ ngày {today.AddDays(1):dd/MM/yyyy} đến ngày {today.AddDays(MaxDaysAhead):dd/MM/yyyy}.");
        }

        var now = clock.UtcNow;
        var tour = await admissions.BookTourAsync(new TourBooking
        {
            Inquiry = new Inquiry
            {
                FullName = request.FullName.Trim(),
                Phone = NormalizePhone(request.Phone),
                Email = Clean(request.Email),
                Source = source,
                Status = InquiryStatus.New,
                CreatedAt = now
            },
            VisitDate = request.VisitDate,
            TimeSlot = slot,
            GuestCount = request.GuestCount,
            Note = Clean(request.Note),
            Status = TourStatus.Requested,
            CreatedAt = now
        }, facility.Value.TourSlotCapacity);

        await notifications.NotifyRolesAsync([Roles.Receptionist], NotificationType.System, "Lịch tham quan mới",
            $"{tour.Inquiry.FullName} — {tour.VisitDate:dd/MM/yyyy}, {slot.ToVietnamese()}, {tour.GuestCount} khách",
            AdmissionsLink + "?tab=tours");
        return await GetTourDtoAsync(tour.Id);
    }

    public async Task<List<TourSlotAvailabilityDto>> GetAvailabilityAsync(DateOnly? from, DateOnly? to)
    {
        var today = clock.Today;
        var first = today.AddDays(1);
        var last = today.AddDays(MaxDaysAhead);
        var start = from is { } requestedFrom && requestedFrom > first ? requestedFrom : first;
        var end = to ?? start.AddDays(13);
        if (end > last)
        {
            end = last;
        }

        if (end < start)
        {
            return [];
        }

        var capacity = facility.Value.TourSlotCapacity;
        var used = (await admissions.CountGuestsAsync(start, end))
            .ToDictionary(usage => (usage.VisitDate, usage.TimeSlot), usage => usage.Guests);
        var result = new List<TourSlotAvailabilityDto>();
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            foreach (var slot in Slots)
            {
                result.Add(new TourSlotAvailabilityDto
                {
                    Date = date,
                    Slot = slot.ToString(),
                    Capacity = capacity,
                    Remaining = Math.Max(0, capacity - used.GetValueOrDefault((date, slot)))
                });
            }
        }

        return result;
    }

    // ---------------- Lễ tân: yêu cầu tư vấn ----------------

    public async Task<List<InquiryDto>> ListInquiriesAsync() =>
        (await admissions.ListInquiriesAsync()).Select(inquiry => inquiry.ToDto()).ToList();

    public async Task<InquiryDto> UpdateInquiryAsync(CurrentUser user, int id, UpdateInquiryRequest request)
    {
        var inquiry = await admissions.GetInquiryAsync(id) ?? throw new NotFoundException("Không tìm thấy yêu cầu tư vấn.");
        var status = ParseEnum<InquiryStatus>(request.Status, "Trạng thái yêu cầu không hợp lệ.");
        if (status != inquiry.Status)
        {
            if (status == InquiryStatus.Admitted)
            {
                throw new BusinessRuleException("Dùng nút \"Nhập viện\" để tạo hồ sơ cư dân và chuyển yêu cầu sang Đã nhập viện.");
            }

            if (!CanTransition(inquiry.Status, status))
            {
                throw new BusinessRuleException($"Không thể chuyển yêu cầu từ \"{Label(inquiry.Status)}\" sang \"{Label(status)}\".");
            }
        }

        var newAssignee = request.AssignedToId is int assigneeId && assigneeId != inquiry.AssignedToId ? assigneeId : (int?)null;
        if (newAssignee is int assignee && !await accounts.IsActiveInRolesAsync(assignee, AssignableRoles))
        {
            throw new BusinessRuleException("Người phụ trách phải là lễ tân hoặc quản trị viên đang hoạt động.");
        }

        inquiry.Status = status;
        inquiry.AssignedToId = request.AssignedToId;
        inquiry.Notes = Clean(request.Notes);
        inquiry.UpdatedAt = clock.UtcNow;
        await admissions.UpdateInquiryAsync(inquiry);

        if (newAssignee is int notifyId && notifyId != user.AccountId)
        {
            await notifications.NotifyAsync([notifyId], NotificationType.System, "Bạn được giao một yêu cầu tư vấn",
                $"{inquiry.FullName} — {inquiry.Phone}", AdmissionsLink);
        }

        return await GetInquiryDtoAsync(id);
    }

    public async Task<InquiryDto> AdmitAsync(int id, AdmitInquiryRequest request)
    {
        var inquiry = await admissions.GetInquiryAsync(id) ?? throw new NotFoundException("Không tìm thấy yêu cầu tư vấn.");
        if (!CanTransition(inquiry.Status, InquiryStatus.Admitted) || inquiry.Status == InquiryStatus.Admitted)
        {
            throw new BusinessRuleException("Chỉ nhập viện từ yêu cầu ở trạng thái \"Đã tham quan\".");
        }

        _ = await residents.GetByIdAsync(request.ResidentId) ?? throw new NotFoundException("Không tìm thấy cư dân.");
        inquiry.Status = InquiryStatus.Admitted;
        inquiry.ResidentId = request.ResidentId;
        inquiry.UpdatedAt = clock.UtcNow;
        await admissions.UpdateInquiryAsync(inquiry);
        return await GetInquiryDtoAsync(id);
    }

    // ---------------- Lễ tân: lịch tham quan ----------------

    public async Task<List<TourBookingDto>> ListToursAsync(DateOnly? from, DateOnly? to)
    {
        var start = from ?? clock.Today;
        var end = to ?? start.AddDays(6);
        if (end < start || end.DayNumber - start.DayNumber > 92)
        {
            throw new BusinessRuleException("Khoảng ngày không hợp lệ (tối đa 93 ngày).");
        }

        return (await admissions.ListToursAsync(start, end)).Select(tour => tour.ToDto()).ToList();
    }

    public async Task<TourBookingDto> ConfirmTourAsync(int id, CurrentUser user)
    {
        var tour = await RequireTourAsync(id);
        if (tour.Status != TourStatus.Requested)
        {
            throw new BusinessRuleException("Chỉ xác nhận được lịch đang chờ xác nhận.");
        }

        if (tour.VisitDate < clock.Today)
        {
            throw new BusinessRuleException("Lịch tham quan đã qua ngày.");
        }

        tour.Status = TourStatus.Confirmed;
        tour.ConfirmedById = user.AccountId;
        tour.UpdatedAt = clock.UtcNow;
        await admissions.UpdateTourAsync(tour);

        var detail = await admissions.GetTourDetailAsync(id) ?? throw new NotFoundException("Không tìm thấy lịch tham quan.");
        await SendConfirmationEmailAsync(detail);
        return detail.ToDto();
    }

    public async Task<TourBookingDto> CancelTourAsync(int id)
    {
        var tour = await RequireOpenTourAsync(id);
        tour.Status = TourStatus.Cancelled;
        tour.UpdatedAt = clock.UtcNow;
        await admissions.UpdateTourAsync(tour);
        return await GetTourDtoAsync(id);
    }

    public async Task<TourBookingDto> CompleteTourAsync(int id)
    {
        var tour = await RequireOpenTourAsync(id);
        RequireVisitDayReached(tour);
        await admissions.CompleteTourAsync(id, clock.UtcNow);
        return await GetTourDtoAsync(id);
    }

    public async Task<TourBookingDto> MarkNoShowAsync(int id)
    {
        var tour = await RequireOpenTourAsync(id);
        RequireVisitDayReached(tour);
        tour.Status = TourStatus.NoShow;
        tour.UpdatedAt = clock.UtcNow;
        await admissions.UpdateTourAsync(tour);
        return await GetTourDtoAsync(id);
    }

    // ---------------- Quy tắc (public static để unit test) ----------------

    /// <summary>Bỏ khoảng trắng, dấu chấm, gạch, ngoặc; còn lại phải là 9–15 chữ số (cho phép + ở đầu).</summary>
    public static string NormalizePhone(string phone)
    {
        var compact = new string((phone ?? "").Where(c => !char.IsWhiteSpace(c) && c is not '.' and not '-' and not '(' and not ')').ToArray());
        return PhonePattern().IsMatch(compact) ? compact : throw new BusinessRuleException("Số điện thoại không hợp lệ.");
    }

    public static bool CanTransition(InquiryStatus from, InquiryStatus to)
    {
        if (from == to)
        {
            return true;
        }

        return from switch
        {
            InquiryStatus.Admitted or InquiryStatus.Lost => false,
            _ when to == InquiryStatus.Lost => true,
            InquiryStatus.New => to is InquiryStatus.Contacted or InquiryStatus.Visited,
            InquiryStatus.Contacted => to == InquiryStatus.Visited,
            InquiryStatus.Visited => to == InquiryStatus.Admitted,
            _ => false
        };
    }

    public static string Label(InquiryStatus status) => status switch
    {
        InquiryStatus.New => "Mới",
        InquiryStatus.Contacted => "Đã liên hệ",
        InquiryStatus.Visited => "Đã tham quan",
        InquiryStatus.Admitted => "Đã nhập viện",
        _ => "Không tiếp tục"
    };

    [GeneratedRegex(@"^\+?\d{9,15}$")]
    private static partial Regex PhonePattern();

    // ---------------- Nội bộ ----------------

    private async Task<InquiryDto> GetInquiryDtoAsync(int id) =>
        (await admissions.GetInquiryDetailAsync(id) ?? throw new NotFoundException("Không tìm thấy yêu cầu tư vấn.")).ToDto();

    private async Task<TourBookingDto> GetTourDtoAsync(int id) =>
        (await admissions.GetTourDetailAsync(id) ?? throw new NotFoundException("Không tìm thấy lịch tham quan.")).ToDto();

    private async Task<TourBooking> RequireTourAsync(int id) =>
        await admissions.GetTourAsync(id) ?? throw new NotFoundException("Không tìm thấy lịch tham quan.");

    private async Task<TourBooking> RequireOpenTourAsync(int id)
    {
        var tour = await RequireTourAsync(id);
        if (!TourBooking.HoldingStatuses.Contains(tour.Status))
        {
            throw new BusinessRuleException("Lịch tham quan đã kết thúc hoặc đã hủy.");
        }

        return tour;
    }

    private void RequireVisitDayReached(TourBooking tour)
    {
        if (tour.VisitDate > clock.Today)
        {
            throw new BusinessRuleException("Chưa đến ngày tham quan.");
        }
    }

    /// <summary>Có email và SMTP đã cấu hình thì gửi; lỗi gửi chỉ ghi log, không hủy việc xác nhận.</summary>
    private async Task SendConfirmationEmailAsync(TourBooking tour)
    {
        if (string.IsNullOrWhiteSpace(tour.Inquiry.Email) || !email.IsConfigured)
        {
            return;
        }

        var options = facility.Value;
        var body = $"""
            <p>Kính gửi {WebUtility.HtmlEncode(tour.Inquiry.FullName)},</p>
            <p>{WebUtility.HtmlEncode(options.Name)} xác nhận lịch tham quan của Quý khách:</p>
            <ul>
              <li>Ngày: <b>{tour.VisitDate:dd/MM/yyyy}</b>, {tour.TimeSlot.ToVietnamese()}</li>
              <li>Số khách: {tour.GuestCount}</li>
              <li>Địa chỉ: {WebUtility.HtmlEncode(options.Address ?? "")}</li>
              <li>Hotline: {WebUtility.HtmlEncode(options.Hotline ?? "")}</li>
            </ul>
            <p>Nếu cần đổi lịch, Quý khách vui lòng gọi hotline. Trân trọng.</p>
            """;
        try
        {
            await email.SendAsync(tour.Inquiry.Email, $"Xác nhận lịch tham quan — {options.Name}", body);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Không gửi được email xác nhận lịch tham quan {TourId}.", tour.Id);
        }
    }

    private static T ParseEnum<T>(string? value, string error) where T : struct, Enum
    {
        var name = Enum.GetNames<T>().FirstOrDefault(item => string.Equals(item, value?.Trim(), StringComparison.OrdinalIgnoreCase));
        return name is null ? throw new BusinessRuleException(error) : Enum.Parse<T>(name);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
