using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NursingHome.Repositories.Implementations;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Ai;
using NursingHome.Services.Implementations;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services;

/// <summary>MODULE CHĂM SÓC &amp; GIA ĐÌNH (Huy): đăng ký Repository + Service của module ở đây.</summary>
public static partial class ServiceCollectionExtensions
{
    static partial void AddCareModule(IServiceCollection services, IConfiguration configuration)
    {
        // S3-H1: sự cố
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IIncidentService, IncidentService>();

        // S3-H2: bệnh nền, dị ứng, chế độ ăn
        services.AddScoped<IMedicalProfileRepository, MedicalProfileRepository>();
        services.AddScoped<IMedicalProfileService, MedicalProfileService>();

        // S3-H3: đặt & duyệt lịch thăm
        services.AddScoped<IVisitRepository, VisitRepository>();
        services.AddScoped<IVisitService, VisitService>();

        // S3-H4: kế hoạch chăm sóc, ADL, bữa ăn, nhắc việc
        services.AddScoped<ICarePlanRepository, CarePlanRepository>();
        services.AddScoped<IDailyLogRepository, DailyLogRepository>();
        services.AddScoped<ITaskReminderRepository, TaskReminderRepository>();
        services.AddScoped<ICarePlanService, CarePlanService>();
        services.AddScoped<IDailyLogService, DailyLogService>();
        services.AddScoped<ITaskReminderService, TaskReminderService>();

        // S3-H5: đánh giá định kỳ, phiếu khám bác sĩ, ngưỡng sinh hiệu riêng
        services.AddScoped<IClinicalRepository, ClinicalRepository>();
        services.AddScoped<IClinicalService, ClinicalService>();

        // S3-H6: nhật ký gia đình & phản hồi
        services.AddScoped<IFamilyCareRepository, FamilyCareRepository>();
        services.AddScoped<IFamilyTimelineService, FamilyTimelineService>();
        services.AddScoped<IFeedbackService, FeedbackService>();

        // S4-H1: trợ lý AI cho gia đình
        services.AddScoped<IFamilyAssistantService, FamilyAssistantService>();

        // S4-H2: nhật ký hằng ngày AI & diễn giải xu hướng sinh hiệu
        services.AddScoped<IAiInsightRepository, AiInsightRepository>();
        services.AddScoped<IDailyDigestService, DailyDigestService>();
        services.AddScoped<IVitalTrendService, VitalTrendService>();
    }
}
