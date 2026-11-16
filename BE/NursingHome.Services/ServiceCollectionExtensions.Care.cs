using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NursingHome.Repositories.Implementations;
using NursingHome.Repositories.Interfaces;
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
    }
}
