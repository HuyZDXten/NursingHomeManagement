using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NursingHome.Services;

/// <summary>MODULE CHĂM SÓC &amp; GIA ĐÌNH (Huy): đăng ký Repository + Service của module ở đây.</summary>
public static partial class ServiceCollectionExtensions
{
    static partial void AddCareModule(IServiceCollection services, IConfiguration configuration)
    {
        // Ví dụ (S3-H1):
        // services.AddScoped<IIncidentRepository, IncidentRepository>();
        // services.AddScoped<IIncidentService, IncidentService>();
    }
}
