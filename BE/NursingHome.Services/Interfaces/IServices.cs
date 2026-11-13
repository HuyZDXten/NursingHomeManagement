using NursingHome.BusinessObjects.Enums;
using NursingHome.DTOs.Accounts;
using NursingHome.DTOs.Auth;
using NursingHome.DTOs.Billing;
using NursingHome.DTOs.Care;
using NursingHome.DTOs.Common;
using NursingHome.DTOs.Medication;
using NursingHome.DTOs.Payments;
using NursingHome.DTOs.Portal;
using NursingHome.DTOs.Residents;
using NursingHome.DTOs.Rooms;

namespace NursingHome.Services.Interfaces;

// ======================= Sprint 0: tài khoản & đăng nhập =======================

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<LoginResponse> RefreshAsync(RefreshTokenRequest request);
    Task LogoutAsync(RefreshTokenRequest request);
    Task ForgotPasswordAsync(ForgotPasswordRequest request);
    Task ResetPasswordAsync(ResetPasswordRequest request);
    Task<LoginResponse> ChangePasswordAsync(CurrentUser user, ChangePasswordRequest request);
    Task<ProfileDto> GetProfileAsync(CurrentUser user);
    Task<ProfileDto> UpdateProfileAsync(CurrentUser user, UpdateProfileRequest request);
}

public interface IAccountService
{
    Task<List<AccountDto>> ListAsync();
    Task<AccountDto> GetAsync(int id);
    Task<AccountDto> CreateAsync(CreateAccountRequest request);
    Task<AccountDto> UpdateAsync(CurrentUser user, int id, UpdateAccountRequest request);
    Task ResetPasswordAsync(int id, AdminResetPasswordRequest request);
    Task<List<LookupItem>> LookupAsync(IReadOnlyCollection<string> roles);
}

// ======================= Sprint 1: nghiệp vụ chuyển từ bản MVC =======================

public enum ResidentSection
{
    Basic,
    Health,
    Care,
    Medication,
    Billing
}

/// <summary>Kiểm tra quyền xem theo từng cư dân (chống IDOR). Gọi ở MỌI service đọc dữ liệu cư dân.</summary>
public interface IResidentAccessService
{
    Task<bool> CanReadAsync(CurrentUser user, int residentId, ResidentSection section);
    Task RequireAsync(CurrentUser user, int residentId, ResidentSection section);
    /// <summary>Null = được xem tất cả; danh sách = chỉ các cư dân này (Caregiver: người đang được giao việc).</summary>
    Task<IReadOnlyCollection<int>?> VisibleResidentIdsAsync(CurrentUser user);
}

public interface IResidentService
{
    Task<List<ResidentListItemDto>> ListAsync(CurrentUser user);
    Task<ResidentDetailDto> GetAsync(CurrentUser user, int id);
    Task<ResidentDetailDto> CreateAsync(SaveResidentRequest request);
    Task<ResidentDetailDto> UpdateAsync(int id, SaveResidentRequest request);
    Task DischargeAsync(CurrentUser user, int id, DischargeRequest request);
    Task<List<ContactDto>> ListContactsAsync(CurrentUser user, int residentId);
    Task<ContactDto> SaveContactAsync(int residentId, int? contactId, SaveContactRequest request);
    Task DeleteContactAsync(int residentId, int contactId);
    Task<List<FamilyLinkDto>> ListLinksAsync(int residentId);
    Task<FamilyLinkDto> SaveLinkAsync(int residentId, SaveFamilyLinkRequest request);
    Task DeleteLinkAsync(int residentId, int linkId);
}

public interface IRoomService
{
    Task<List<RoomDto>> ListAsync();
    Task<RoomDto> GetAsync(int id);
    Task<RoomDto> SaveAsync(int? id, SaveRoomRequest request);
    Task DeleteAsync(int id);
    Task<BedDto> SaveBedAsync(int roomId, int? bedId, SaveBedRequest request);
    Task DeleteBedAsync(int bedId);
    Task AssignAsync(CurrentUser user, AssignBedRequest request);
    Task ReleaseAsync(int residentId);
    Task<List<BedAssignmentDto>> HistoryAsync(int? residentId);
}

public interface ICareTaskService
{
    Task<List<CareTaskDto>> ListAsync(CurrentUser user, DateOnly? date, bool mine, int? residentId);
    Task<CareTaskDto> GetAsync(CurrentUser user, int id);
    Task<CareTaskDto> CreateAsync(CurrentUser user, CreateCareTaskRequest request);
    Task<CareTaskDto> UpdateAsync(CurrentUser user, int id, UpdateCareTaskRequest request);
    Task<List<CareRecordDto>> ListRecordsAsync(CurrentUser user, int id);
}

public interface IHealthService
{
    Task<List<VitalSignDto>> ListAsync(CurrentUser user, int residentId, DateOnly? from, DateOnly? to);
    Task<VitalSignDto> RecordAsync(CurrentUser user, RecordVitalSignRequest request);
}

public interface IMedicationService
{
    Task<List<MedicationScheduleDto>> ListSchedulesAsync(CurrentUser user, int? residentId);
    Task<MedicationScheduleDto> CreateScheduleAsync(CurrentUser user, CreateMedicationScheduleRequest request);
    Task StopAsync(int id);
    Task<List<MedicationDoseDto>> TodayAsync(CurrentUser user, DateOnly? date);
    Task RecordAsync(CurrentUser user, RecordMedicationRequest request);
    Task<List<MedicationLogDto>> HistoryAsync(CurrentUser user, int residentId);
}

public interface IBillingService
{
    Task<List<ServicePackageDto>> ListPackagesAsync(bool activeOnly);
    Task<ServicePackageDto> SavePackageAsync(int? id, SavePackageRequest request);
    Task<List<SubscriptionDto>> ListSubscriptionsAsync(int? residentId);
    Task<SubscriptionDto> SubscribeAsync(CreateSubscriptionRequest request);
    Task EndSubscriptionAsync(int id);
    Task<List<InvoiceListItemDto>> ListInvoicesAsync(int? residentId);
    Task<InvoiceDetailDto> GetInvoiceAsync(CurrentUser user, int id);
    Task<InvoiceDetailDto> CreateInvoiceAsync(CreateInvoiceRequest request);
    Task<InvoiceDetailDto> AddItemAsync(int id, AddInvoiceItemRequest request);
    Task<InvoiceDetailDto> IssueAsync(int id);
    Task<InvoiceDetailDto> RecordPaymentAsync(CurrentUser user, int id, RecordPaymentRequest request);
    Task CancelAsync(int id);
}

public interface IFamilyService
{
    Task<List<FamilyResidentDto>> MyResidentsAsync(CurrentUser user);
    Task<FamilyResidentDetailDto> DetailsAsync(CurrentUser user, int residentId);
}

public interface IDashboardService
{
    Task<DashboardDto> GetAsync();
    Task<PublicOverviewDto> GetPublicOverviewAsync();
}

// ======================= Sprint 2: PayOS & thông báo =======================

public interface IPaymentService
{
    Task<CheckoutResponse> CreateCheckoutAsync(CurrentUser user, int invoiceId);
    Task<PaymentStatusDto> GetStatusAsync(CurrentUser user, long transactionId);
    /// <summary>Lớp 1: webhook payOS. Luôn trả kết quả (không ném) để API trả 200 cho payOS.</summary>
    Task<string> HandleWebhookAsync(string rawPayload);
    /// <summary>Lớp 2: hỏi payOS trạng thái một giao dịch.</summary>
    Task<PaymentStatusDto> SyncAsync(long transactionId);
    Task<int> SyncPendingAsync(CancellationToken cancellationToken);
    Task<List<PaymentTransactionDto>> ListTransactionsAsync(string? status);
    Task<List<WebhookLogDto>> ListLogsAsync();
}

public interface INotificationService
{
    Task NotifyAsync(IEnumerable<int> accountIds, NotificationType type, string title, string? body, string? link);
    Task NotifyRolesAsync(IReadOnlyCollection<string> roles, NotificationType type, string title, string? body, string? link);
    Task<NotificationSummaryDto> ListAsync(CurrentUser user);
    Task MarkReadAsync(CurrentUser user, long id);
    Task MarkAllReadAsync(CurrentUser user);
}

/// <summary>Đẩy thông báo realtime. API cài đặt bằng SignalR; Services không phụ thuộc SignalR.</summary>
public interface INotificationPublisher
{
    Task PublishAsync(IReadOnlyCollection<int> accountIds, NotificationDto notification);
}

public interface IEmailService
{
    bool IsConfigured { get; }
    Task SendAsync(string toEmail, string subject, string htmlBody);
}
