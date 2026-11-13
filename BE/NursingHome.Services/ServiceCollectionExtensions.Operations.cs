using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NursingHome.Repositories.Implementations;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Implementations;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services;

/// <summary>MODULE VẬN HÀNH &amp; TÀI CHÍNH (Viên): đăng ký Repository + Service của module ở đây.</summary>
public static partial class ServiceCollectionExtensions
{
    static partial void AddOperationsModule(IServiceCollection services, IConfiguration configuration)
    {
        // S3-V1 tiếp nhận & tham quan
        services.AddScoped<IAdmissionRepository, AdmissionRepository>();
        services.AddScoped<IAdmissionService, AdmissionService>();
    }
}
