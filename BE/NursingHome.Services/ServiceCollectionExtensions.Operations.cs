using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NursingHome.Repositories.Implementations;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Ai;
using NursingHome.Services.Implementations;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.Services;

/// <summary>MODULE VẬN HÀNH &amp; TÀI CHÍNH (Viên): đăng ký Repository + Service của module ở đây.</summary>
public static partial class ServiceCollectionExtensions
{
    static partial void AddOperationsModule(IServiceCollection services, IConfiguration configuration)
    {
        // S3-V1 tiếp nhận & tham quan
        services.AddScoped<IAdmissionRepository, AdmissionRepository>();
        services.AddScoped<IAdmissionService, AdmissionService>();

        // S3-V2 ca trực & phân công phụ trách (+ S3-V4 nghỉ phép/đổi ca, bàn giao ca)
        services.AddScoped<IStaffingRepository, StaffingRepository>();
        services.AddScoped<IStaffingService, StaffingService>();
        services.AddScoped<IHandoverService, HandoverService>();

        // S3-V3 báo cáo
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IReportService, ReportService>();

        // S3-V5 dịch vụ phát sinh, hóa đơn hàng loạt, PDF, nhắc nợ, email
        services.Configure<BillingJobOptions>(configuration.GetSection(BillingJobOptions.SectionName));
        services.AddScoped<IExtraServiceRepository, ExtraServiceRepository>();
        services.AddScoped<IReminderRepository, ReminderRepository>();
        services.AddScoped<IExtraServiceService, ExtraServiceService>();
        services.AddScoped<IInvoiceMailer, InvoiceMailer>();
        services.AddScoped<IInvoiceAutomationService, InvoiceAutomationService>();

        // S3-V6 chat gia đình – nhân viên (IChatPublisher cài ở API: SignalRChatPublisher)
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IChatService, ChatService>();

        // S4-V1 hạ tầng AI dùng chung: Ai:Provider = "Fake" (không gọi mạng) hoặc "Gemini" (cần key ở user-secrets)
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.AddScoped<IAiRepository, AiRepository>();
        services.AddHttpClient<GeminiChatProvider>((provider, client) =>
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(provider.GetRequiredService<IOptions<AiOptions>>().Value.TimeoutSeconds, 5, 60)));
        services.AddSingleton<FakeAiChatProvider>();
        services.AddScoped<IAiChatProvider>(provider => provider.GetRequiredService<IOptions<AiOptions>>().Value.UsesFake
            ? provider.GetRequiredService<FakeAiChatProvider>()
            : provider.GetRequiredService<GeminiChatProvider>());
        services.AddScoped<IAiChatEngine, AiChatEngine>();
        services.AddScoped<IPublicChatbotService, PublicChatbotService>();

        // S4-V2 AI tóm tắt bàn giao ca (AI-04) + hỏi đáp báo cáo (AI-12)
        services.AddScoped<IHandoverAiService, HandoverAiService>();
        services.AddScoped<IReportAssistantService, ReportAssistantService>();

        // BL-V1 ảnh (UC-07, UC-24), đăng nhập Google (UC-02), mã mời gia đình (UC-08).
        // IImageStorage chọn ở API (AddImageStorage): Cloudinary khi đủ 3 khóa, không thì wwwroot/uploads.
        services.Configure<CloudinaryOptions>(configuration.GetSection(CloudinaryOptions.SectionName));
        services.Configure<GoogleOptions>(configuration.GetSection(GoogleOptions.SectionName));
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IFamilyInvitationRepository, FamilyInvitationRepository>();
        services.AddScoped<IFamilyInvitationService, FamilyInvitationService>();

        // BL-V2 hồ sơ lưu trú: giấy tờ, hợp đồng, tạm vắng, tái nhập viện, tiền cọc (IDocumentStorage cài ở API).
        services.AddScoped<IResidentRecordsRepository, ResidentRecordsRepository>();
        services.AddScoped<IResidentRecordsService, ResidentRecordsService>();

        // BL-V3 loại phòng & phụ thu (BillingService, RoomService dùng IRoomOpsRepository), giữ giường, báo hỏng.
        services.AddScoped<IRoomOpsRepository, RoomOpsRepository>();
        services.AddScoped<IRoomOpsService, RoomOpsService>();

        // BL-V4 nhân sự: hồ sơ & chứng chỉ, chấm công & hiệu suất, trang chủ cá nhân, trợ lý nhân viên (AI-05).
        services.Configure<StaffJobOptions>(configuration.GetSection(StaffJobOptions.SectionName));
        services.AddScoped<IStaffHrRepository, StaffHrRepository>();
        services.AddScoped<IStaffHrService, StaffHrService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IStaffHomeService, StaffHomeService>();
        services.AddScoped<IStaffAssistantService, StaffAssistantService>();

        // BL-V5 hệ thống & truyền thông: nhật ký thao tác, cấu hình cơ sở, danh mục, thông báo hàng loạt, kênh nhận, tin tức & album.
        // FacilitySettingsStore thay IOptions<FacilityOptions> mặc định: mọi service đọc cấu hình cơ sở nhận ngay giá trị Admin vừa sửa.
        services.AddSingleton<FacilitySettingsStore>();
        services.AddSingleton<IOptions<FacilityOptions>>(provider => provider.GetRequiredService<FacilitySettingsStore>());
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<ISystemRepository, SystemRepository>();
        services.AddScoped<ICommsRepository, CommsRepository>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();
        services.AddScoped<IPostService, PostService>();
    }
}
