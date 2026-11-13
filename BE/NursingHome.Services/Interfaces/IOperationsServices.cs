using NursingHome.BusinessObjects.Entities;
using NursingHome.DTOs.Admissions;
using NursingHome.DTOs.Billing;
using NursingHome.DTOs.Chat;
using NursingHome.DTOs.Common;
using NursingHome.DTOs.Reports;
using NursingHome.DTOs.Staffing;

namespace NursingHome.Services.Interfaces;

// MODULE VẬN HÀNH & TÀI CHÍNH (Viên): khai báo interface service của module ở file này
// (không thêm vào IServices.cs của phần lõi).

/// <summary>S3-V1 — UC-17, 18, 21, 22: yêu cầu tư vấn, lịch tham quan, phễu khách.</summary>
public interface IAdmissionService
{
    /// <summary>Ẩn danh hoặc chatbot (S4-V1 gọi với <see cref="InquirySource.Chatbot"/>).</summary>
    Task<InquiryDto> CreateInquiryAsync(CreateInquiryRequest request, InquirySource source);

    /// <summary>Ẩn danh: tạo yêu cầu + lịch tham quan, kiểm sức chứa buổi trong transaction.</summary>
    Task<TourBookingDto> BookTourAsync(BookTourRequest request, InquirySource source = InquirySource.Web);

    /// <summary>Số chỗ còn lại mỗi ngày × buổi trong [from, to] (kẹp vào [mai, +60 ngày]).</summary>
    Task<List<TourSlotAvailabilityDto>> GetAvailabilityAsync(DateOnly? from, DateOnly? to);

    Task<List<InquiryDto>> ListInquiriesAsync();

    Task<InquiryDto> UpdateInquiryAsync(CurrentUser user, int id, UpdateInquiryRequest request);

    Task<InquiryDto> AdmitAsync(int id, AdmitInquiryRequest request);

    Task<List<TourBookingDto>> ListToursAsync(DateOnly? from, DateOnly? to);

    Task<TourBookingDto> ConfirmTourAsync(int id, CurrentUser user);

    Task<TourBookingDto> CancelTourAsync(int id);

    Task<TourBookingDto> CompleteTourAsync(int id);

    Task<TourBookingDto> MarkNoShowAsync(int id);
}

/// <summary>S3-V2 — UC-44 lịch ca, UC-45 phân công phụ trách, UC-46 lịch ca của tôi.</summary>
public interface IStaffingService
{
    Task<List<ShiftDto>> ListShiftsAsync();

    /// <summary>Admin/Nurse thấy mọi người; Doctor/Caregiver chỉ ca của mình.</summary>
    Task<List<ShiftAssignmentDto>> ListAssignmentsAsync(CurrentUser user, DateOnly? from, DateOnly? to);

    Task<List<ShiftAssignmentDto>> MyAssignmentsAsync(CurrentUser user, int days);

    Task<ShiftAssignmentDto> CreateAssignmentAsync(CurrentUser user, CreateShiftAssignmentRequest request);

    Task DeleteAssignmentAsync(int id);

    Task<CopyWeekResultDto> CopyWeekAsync(CurrentUser user, CopyWeekRequest request);

    /// <summary>Caregiver chỉ thấy phân công của mình.</summary>
    Task<List<CareAssignmentDto>> ListCareAssignmentsAsync(CurrentUser user, int? residentId, int? accountId, bool activeOnly);

    Task<CareAssignmentDto> CreateCareAssignmentAsync(CurrentUser user, CreateCareAssignmentRequest request);

    Task<CareAssignmentDto> EndCareAssignmentAsync(int id);
}

/// <summary>S3-V3 — UC-123 doanh thu &amp; công nợ, UC-122 công suất giường, UC-124 tuân thủ thuốc. Mỗi báo cáo có bản Excel.</summary>
public interface IReportService
{
    Task<RevenueReportDto> GetRevenueAsync(int? year);

    Task<ExportFile> ExportRevenueAsync(int? year);

    Task<OccupancyReportDto> GetOccupancyAsync(int? year);

    Task<ExportFile> ExportOccupancyAsync(int? year);

    Task<AdherenceReportDto> GetAdherenceAsync(DateOnly? from, DateOnly? to);

    Task<ExportFile> ExportAdherenceAsync(DateOnly? from, DateOnly? to);
}

/// <summary>S3-V4 — UC-47 nghỉ phép/đổi ca (Admin duyệt), UC-49 bàn giao ca (gợi ý, xác nhận đã đọc).</summary>
public interface IHandoverService
{
    /// <summary>Admin thấy mọi đơn; người khác chỉ đơn của mình hoặc đơn xin đổi ca với mình.</summary>
    Task<List<LeaveRequestDto>> ListLeaveRequestsAsync(CurrentUser user, string? status);

    Task<LeaveRequestDto> CreateLeaveRequestAsync(CurrentUser user, CreateLeaveRequest request);

    /// <summary>Ca của đồng nghiệp cùng role đổi được lấy ca của mình (4 tuần tới).</summary>
    Task<List<ShiftAssignmentDto>> SwapCandidatesAsync(CurrentUser user, int shiftAssignmentId);

    /// <summary>Đổi ca: hoán 2 ca trong một transaction. Nghỉ: bỏ ca chưa bắt đầu trong khoảng, trả danh sách ca trống.</summary>
    Task<LeaveApprovalDto> ApproveAsync(CurrentUser user, int id, string? note);

    Task<LeaveRequestDto> RejectAsync(CurrentUser user, int id, string? note);

    Task<LeaveRequestDto> CancelAsync(CurrentUser user, int id);

    Task<HandoverDraftDto> GetDraftAsync(CurrentUser user, int shiftId, DateOnly? date);

    Task<HandoverDto> CreateHandoverAsync(CurrentUser user, CreateHandoverRequest request);

    Task<List<HandoverDto>> ListHandoversAsync(CurrentUser user, DateOnly? date);

    Task<List<HandoverDto>> InboxAsync(CurrentUser user);

    Task<HandoverDto> AcknowledgeAsync(CurrentUser user, long id);
}

/// <summary>S3-V5 — UC-85 danh mục dịch vụ phát sinh, UC-86 ghi lần sử dụng (giá chốt lúc ghi).</summary>
public interface IExtraServiceService
{
    Task<List<ExtraServiceDto>> ListServicesAsync(bool activeOnly);

    Task<ExtraServiceDto> SaveServiceAsync(int? id, SaveExtraServiceRequest request);

    /// <summary>Không chọn cư dân = mọi cư dân người gọi được xem; mặc định từ đầu tháng tới hôm nay.</summary>
    Task<List<ServiceUsageDto>> ListUsagesAsync(CurrentUser user, int? residentId, DateOnly? from, DateOnly? to);

    Task<ServiceUsageDto> RecordUsageAsync(CurrentUser user, int residentId, CreateServiceUsageRequest request);

    Task DeleteUsageAsync(CurrentUser user, int id);
}

/// <summary>S3-V5 — UC-98 hóa đơn hàng loạt, UC-97 nhắc nợ, UC-88 PDF.</summary>
public interface IInvoiceAutomationService
{
    Task<BatchInvoiceResultDto> CreateBatchAsync(CreateBatchInvoiceRequest request);

    /// <summary>Job mỗi giờ (InvoiceReminderJob) và nút "Chạy nhắc nợ" của kế toán.</summary>
    Task<ReminderRunResultDto> SendRemindersAsync(CancellationToken cancellationToken = default);

    Task<List<InvoiceReminderDto>> ListRemindersAsync(int invoiceId);

    Task<ExportFile> GetPdfAsync(CurrentUser user, int id);
}

/// <summary>S3-V5 — UC-118 email hóa đơn cho gia đình có quyền Billing (chỉ khi SMTP đã cấu hình). Trả số email đã gửi.</summary>
public interface IInvoiceMailer
{
    Task<int> SendIssuedAsync(InvoiceListItemDto invoice);

    Task<int> SendReminderAsync(InvoiceListItemDto invoice, ReminderStage stage);
}

/// <summary>S3-V6 — UC-106 chat gia đình – nhân viên.</summary>
public interface IChatService
{
    /// <summary>Family: cuộc trò chuyện của mình. Nhân viên: theo quyền (Caregiver chỉ cư dân phụ trách chính).</summary>
    Task<List<ConversationDto>> ListAsync(CurrentUser user);

    /// <summary>Family mở (hoặc lấy lại) cuộc trò chuyện về một người thân được liên kết.</summary>
    Task<ConversationDto> OpenAsync(CurrentUser user, int residentId);

    Task<List<ChatMessageDto>> MessagesAsync(CurrentUser user, int conversationId, long? beforeId, int take);

    Task<ChatMessageDto> SendAsync(CurrentUser user, int conversationId, SendMessageRequest request);

    Task<int> MarkReadAsync(CurrentUser user, int conversationId);
}

/// <summary>Đẩy tin nhắn realtime (sự kiện "chat" trên hub /hubs/notifications). API cài bằng SignalR.</summary>
public interface IChatPublisher
{
    Task PublishMessageAsync(IReadOnlyCollection<int> accountIds, ChatMessageDto message);
}
