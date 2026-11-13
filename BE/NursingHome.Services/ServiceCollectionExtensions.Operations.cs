using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NursingHome.Services;

/// <summary>MODULE VẬN HÀNH &amp; TÀI CHÍNH (Viên): đăng ký Repository + Service của module ở đây.</summary>
public static partial class ServiceCollectionExtensions
{
    static partial void AddOperationsModule(IServiceCollection services, IConfiguration configuration)
    {
        // Ví dụ (S3-V1):
        // services.AddScoped<IAdmissionRepository, AdmissionRepository>();
        // services.AddScoped<IAdmissionService, AdmissionService>();
    }
}
