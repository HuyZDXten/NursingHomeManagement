using NursingHome.BusinessObjects.Entities;
using NursingHome.DTOs.Accounts;
using NursingHome.DTOs.Admissions;
using NursingHome.DTOs.Auth;
using NursingHome.DTOs.Billing;
using NursingHome.DTOs.Chat;
using NursingHome.DTOs.Common;
using NursingHome.DTOs.Reports;
using NursingHome.DTOs.Residents;
using NursingHome.DTOs.Staffing;

namespace NursingHome.Services.Interfaces;

// ======================= BL-V1: ảnh, đăng nhập Google, mã mời gia đình =======================

/// <summary>
/// Nơi lưu ảnh: Cloudinary (đủ 3 khóa) hoặc thư mục wwwroot/uploads của API (cài ở API, vì cần môi trường web).
/// Trả URL tuyệt đối (Cloudinary) hoặc đường dẫn "/uploads/..." (FE ghép địa chỉ API).
/// </summary>
public interface IImageStorage
{
    Task<string> SaveAsync(Stream content, string extension, string folder, CancellationToken cancellationToken = default);
}

/// <param name="EmailVerified">Google chỉ bảo đảm email khi cờ này = true.</param>
public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified, string? Name);

public interface IGoogleTokenValidator
{
    /// <summary>Kiểm chữ ký, hạn, issuer và audience = Client ID của mình. Sai → 401; chưa cấu hình → 503.</summary>
    Task<GoogleIdentity> ValidateAsync(string idToken);
}

/// <summary>UC-07 ảnh đại diện, UC-24 ảnh chân dung cư dân, ảnh dùng chung (Huy dùng cho UC-60).</summary>
public interface IMediaService
{
    Task<UploadedImageDto> UploadImageAsync(Stream content, string? fileName, long length, string folder);

    Task<ProfileDto> SetMyAvatarAsync(CurrentUser user, Stream content, string? fileName, long length);

    Task<ProfileDto> RemoveMyAvatarAsync(CurrentUser user);

    Task<ResidentDetailDto> SetResidentPhotoAsync(CurrentUser user, int residentId, Stream content, string? fileName, long length);

    Task<ResidentDetailDto> RemoveResidentPhotoAsync(CurrentUser user, int residentId);
}

/// <summary>UC-08: lễ tân mời người thân; người thân tự đặt mật khẩu qua /kich-hoat?code=.</summary>
public interface IFamilyInvitationService
{
    Task<FamilyInvitationCreatedDto> CreateAsync(CurrentUser user, CreateFamilyInvitationRequest request);

    Task<List<FamilyInvitationDto>> ListAsync(int? residentId);

    Task<FamilyInvitationDto> RevokeAsync(int id);

    Task<InvitationLookupDto> LookupAsync(string code);

    /// <summary>Tạo tài khoản Family + liên kết cư dân, rồi đăng nhập luôn.</summary>
    Task<LoginResponse> AcceptAsync(AcceptInvitationRequest request);
}

// ======================= BL-V2: hồ sơ lưu trú =======================

/// <summary>File giấy tờ/hợp đồng: lưu RIÊNG TƯ (không phục vụ tĩnh), chỉ đọc qua endpoint có kiểm quyền. Cài ở API.</summary>
public interface IDocumentStorage
{
    /// <summary>Trả tên lưu (GUID + đuôi).</summary>
    Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default);

    Stream OpenRead(string storedName);

    void Delete(string storedName);
}

/// <summary>Nội dung file trả về cho controller (FileStreamResult).</summary>
public sealed record StoredFile(Stream Content, string ContentType, string FileName);

/// <summary>UC-29 giấy tờ, UC-30 hợp đồng, UC-31 tạm vắng, UC-33 tái nhập viện, UC-99 tiền cọc.</summary>
public interface IResidentRecordsService
{
    Task<List<ResidentDocumentDto>> ListDocumentsAsync(int residentId);

    Task<ResidentDocumentDto> UploadDocumentAsync(CurrentUser user, int residentId, UploadDocumentRequest request, Stream content, string? fileName, long length);

    Task<StoredFile> OpenDocumentAsync(int residentId, int documentId);

    Task DeleteDocumentAsync(int residentId, int documentId);

    /// <summary>Kiểm phần Billing của cư dân: lễ tân, kế toán, Admin, gia đình có quyền xem hóa đơn.</summary>
    Task<List<ResidentContractDto>> ListContractsAsync(CurrentUser user, int residentId);

    Task<ResidentContractDto> CreateContractAsync(CurrentUser user, int residentId, CreateContractRequest request, Stream? content, string? fileName, long length);

    Task<ResidentContractDto> EndContractAsync(int residentId, int contractId, EndContractRequest request);

    Task<StoredFile> OpenContractFileAsync(CurrentUser user, int residentId, int contractId);

    Task<List<ResidentLeaveDto>> ListLeavesAsync(CurrentUser user, int residentId);

    /// <summary>Cư dân đang tạm vắng (lọc theo cư dân người xem được).</summary>
    Task<List<ResidentLeaveDto>> OpenLeavesAsync(CurrentUser user);

    Task<ResidentLeaveDto> StartLeaveAsync(CurrentUser user, int residentId, StartLeaveRequest request);

    Task<ResidentLeaveDto> ReturnFromLeaveAsync(CurrentUser user, int residentId, int leaveId, ReturnLeaveRequest request);

    Task<List<ResidentStayDto>> ListStaysAsync(int residentId);

    Task<ResidentDetailDto> ReadmitAsync(CurrentUser user, int residentId, ReadmitRequest request);

    Task<DepositSummaryDto> DepositsAsync(int residentId);

    Task<DepositSummaryDto> CreateDepositAsync(CurrentUser user, int residentId, CreateDepositRequest request);

    Task<DepositSummaryDto> RefundDepositAsync(CurrentUser user, int depositId, RefundDepositRequest request);
}

// ======================= BL-V3: phòng & giường mở rộng =======================

/// <summary>UC-36 loại phòng & phụ thu, UC-41 giữ giường, UC-42 báo hỏng & sửa chữa.</summary>
public interface IRoomOpsService
{
    Task<List<NursingHome.DTOs.Rooms.RoomTypeDto>> ListTypesAsync(bool activeOnly);

    Task<NursingHome.DTOs.Rooms.RoomTypeDto> SaveTypeAsync(int? id, NursingHome.DTOs.Rooms.SaveRoomTypeRequest request);

    Task<List<NursingHome.DTOs.Rooms.BedReservationDto>> ListReservationsAsync(bool activeOnly);

    Task<NursingHome.DTOs.Rooms.BedReservationDto> CreateReservationAsync(CurrentUser user, NursingHome.DTOs.Rooms.CreateBedReservationRequest request);

    Task<NursingHome.DTOs.Rooms.BedReservationDto> ReleaseReservationAsync(CurrentUser user, int id);

    Task<List<NursingHome.DTOs.Rooms.MaintenanceRequestDto>> ListMaintenanceAsync(string? status, int? roomId);

    Task<NursingHome.DTOs.Rooms.MaintenanceRequestDto> CreateMaintenanceAsync(CurrentUser user, NursingHome.DTOs.Rooms.CreateMaintenanceRequest request);

    Task<NursingHome.DTOs.Rooms.MaintenanceRequestDto> UpdateMaintenanceAsync(CurrentUser user, int id, NursingHome.DTOs.Rooms.UpdateMaintenanceRequest request);
}

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

    /// <summary>UC-125: sự cố theo tháng × loại × mức độ (khoảng ngày mặc định 6 tháng gần nhất, tối đa 366 ngày).</summary>
    Task<IncidentReportDto> GetIncidentsAsync(DateOnly? from, DateOnly? to);

    Task<ExportFile> ExportIncidentsAsync(DateOnly? from, DateOnly? to);
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

// ======================= BL-V4: nhân sự =======================

/// <summary>UC-43: hồ sơ nhân viên, chứng chỉ, cảnh báo chứng chỉ sắp hết hạn.</summary>
public interface IStaffHrService
{
    Task<List<StaffProfileSummaryDto>> ListProfilesAsync();

    /// <summary>Admin xem mọi người; nhân viên chỉ xem hồ sơ của chính mình (Forbidden nếu xem người khác).</summary>
    Task<StaffProfileDto> GetProfileAsync(CurrentUser user, int accountId);

    Task<StaffProfileDto> SaveProfileAsync(int accountId, SaveStaffProfileRequest request);

    Task<StaffCertificateDto> AddCertificateAsync(CurrentUser user, int accountId, SaveCertificateRequest request);

    Task<StaffCertificateDto> UpdateCertificateAsync(int id, SaveCertificateRequest request);

    Task DeleteCertificateAsync(int id);

    /// <summary>Chứng chỉ còn ≤ 30 ngày hoặc đã hết hạn của nhân viên đang hoạt động.</summary>
    Task<List<StaffCertificateDto>> ListExpiringAsync();

    /// <summary>Gửi thông báo các mốc chưa báo (còn ≤ 30 ngày, đã hết hạn); mỗi mốc một lần. Job nền và Admin đều gọi được.</summary>
    Task<CertificateAlertResultDto> SendExpiryAlertsAsync(CancellationToken cancellationToken = default);
}

/// <summary>UC-48 chấm công vào/ra ca, UC-126 hiệu suất nhân viên.</summary>
public interface IAttendanceService
{
    /// <summary>Ca của tôi từ hôm qua tới 7 ngày tới (ca đêm hôm qua có thể còn đang làm).</summary>
    Task<List<MyShiftDto>> MyShiftsAsync(CurrentUser user);

    Task<MyShiftDto> CheckInAsync(CurrentUser user, int shiftAssignmentId);

    Task<MyShiftDto> CheckOutAsync(CurrentUser user, int shiftAssignmentId);

    Task<List<AttendanceRecordDto>> ListAsync(DateOnly? from, DateOnly? to, int? accountId);

    Task<AttendanceRecordDto> EditAsync(CurrentUser admin, int shiftAssignmentId, EditAttendanceRequest request);

    Task<StaffPerformanceReportDto> GetPerformanceAsync(DateOnly? from, DateOnly? to);

    Task<ExportFile> ExportPerformanceAsync(DateOnly? from, DateOnly? to);
}

/// <summary>UC-127: trang chủ cá nhân (việc hôm nay, ca kế tiếp, bàn giao và tin nhắn chưa đọc, chứng chỉ sắp hết hạn).</summary>
public interface IStaffHomeService
{
    Task<StaffHomeDto> GetAsync(CurrentUser user);

    /// <summary>Việc của tôi hôm nay (dùng cho tool AI my_tasks_today).</summary>
    Task<List<HomeTaskDto>> TasksTodayAsync(CurrentUser user);
}
