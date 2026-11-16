using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using NursingHome.API.Storage;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.Services.Helpers;
using NursingHome.Services.Implementations;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.API.Extensions;

/// <summary>BL-V1: chọn nơi lưu ảnh và phục vụ ảnh lưu local.</summary>
public static class MediaExtensions
{
    /// <summary>Đủ 3 khóa Cloudinary → Cloudinary; thiếu → wwwroot/uploads (dev/demo không cần tài khoản).</summary>
    public static IServiceCollection AddImageStorage(this IServiceCollection services)
    {
        services.AddSingleton<CloudinaryImageStorage>();
        services.AddSingleton<LocalImageStorage>();
        services.AddSingleton<IImageStorage>(provider =>
            provider.GetRequiredService<IOptions<CloudinaryOptions>>().Value.IsConfigured
                ? provider.GetRequiredService<CloudinaryImageStorage>()
                : provider.GetRequiredService<LocalImageStorage>());
        // BL-V2: giấy tờ/hợp đồng luôn lưu riêng tư trên máy (App_Data/documents), không lên Cloudinary.
        services.AddSingleton<IDocumentStorage, LocalDocumentStorage>();
        return services;
    }

    /// <summary>Phục vụ /uploads/... (chỉ ảnh đã qua kiểm tra chữ ký). Tên file là GUID nên cache lâu được.</summary>
    public static IApplicationBuilder UseUploadedImages(this WebApplication app)
    {
        string root = LocalImageStorage.RootPath(app.Environment);
        Directory.CreateDirectory(root);
        return app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(root),
            RequestPath = LocalImageStorage.RequestPath,
            OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "public, max-age=604800, immutable"
        });
    }

    /// <summary>
    /// Chép file upload vào bộ nhớ (≤ 5 MB) để service đọc chữ ký rồi tua lại. File quá cỡ không chép:
    /// trả stream rỗng, service báo "Ảnh tối đa 5 MB" theo độ dài.
    /// </summary>
    public static async Task<Stream> ReadImageAsync(this IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            throw new BusinessRuleException("Chưa chọn ảnh.");
        }

        if (file.Length > ImageRules.MaxBytes)
        {
            return Stream.Null;
        }

        var memory = new MemoryStream((int)file.Length);
        await file.CopyToAsync(memory);
        memory.Position = 0;
        return memory;
    }
}
