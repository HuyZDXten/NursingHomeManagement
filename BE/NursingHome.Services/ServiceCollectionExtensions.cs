using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NursingHome.Repositories.Implementations;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Implementations;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;
using NursingHome.Services.Payments;

namespace NursingHome.Services;

/// <remarks>
/// partial: module Sprint 3+ đăng ký trong file riêng (ServiceCollectionExtensions.Operations.cs — Viên,
/// ServiceCollectionExtensions.Care.cs — Huy), không sửa file này.
/// </remarks>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký toàn bộ Repositories + Services của phần lõi (Sprint 0–2) và gọi đăng ký của 2 module.
    /// (INotificationPublisher do API đăng ký vì dùng SignalR.)
    /// </summary>
    public static IServiceCollection AddNursingHomeServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<PayOsOptions>(configuration.GetSection(PayOsOptions.SectionName));
        services.Configure<FacilityOptions>(configuration.GetSection(FacilityOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<FacilityClock>();
        services.AddSingleton<PasswordResetOtpStore>();
        services.AddMemoryCache();
        services.AddHttpClient<IPayOsClient, PayOsClient>(client => client.Timeout = TimeSpan.FromSeconds(15));

        // ---- Repositories (ủy quyền cho DAO singleton) ----
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IResidentRepository, ResidentRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<ICareRepository, CareRepository>();
        services.AddScoped<IHealthRepository, HealthRepository>();
        services.AddScoped<IMedicationRepository, MedicationRepository>();
        services.AddScoped<IBillingRepository, BillingRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();

        // ---- Services ----
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IResidentAccessService, ResidentAccessService>();
        services.AddScoped<IResidentService, ResidentService>();
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<ICareTaskService, CareTaskService>();
        services.AddScoped<IHealthService, HealthService>();
        services.AddScoped<IMedicationService, MedicationService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IFamilyService, FamilyService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IPaymentService, PaymentService>();

        // ---- Module Sprint 3+ ----
        AddOperationsModule(services, configuration);
        AddCareModule(services, configuration);
        return services;
    }

    static partial void AddOperationsModule(IServiceCollection services, IConfiguration configuration);

    static partial void AddCareModule(IServiceCollection services, IConfiguration configuration);
}
