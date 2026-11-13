using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NursingHome.Repositories.Implementations;
using NursingHome.Repositories.Interfaces;
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
    }
}
