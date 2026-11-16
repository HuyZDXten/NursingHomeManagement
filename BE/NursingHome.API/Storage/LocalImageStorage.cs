using NursingHome.Services.Interfaces;

namespace NursingHome.API.Storage;

/// <summary>
/// BL-V1 (chép ý từ ASS2): khi chưa cấu hình Cloudinary, ảnh lưu ở {ContentRoot}/wwwroot/uploads/{folder}/
/// và được phục vụ tĩnh ở /uploads/... Trả đường dẫn TƯƠNG ĐỐI (/uploads/...) để đổi cổng/máy không hỏng link;
/// FE ghép địa chỉ API (utils/media.ts). Tên file = GUID + đuôi theo chữ ký ảnh, không dùng tên người dùng gửi.
/// </summary>
public class LocalImageStorage(IWebHostEnvironment environment) : IImageStorage
{
    public const string RequestPath = "/uploads";

    public static string RootPath(IWebHostEnvironment environment) => Path.Combine(environment.ContentRootPath, "wwwroot", "uploads");

    public async Task<string> SaveAsync(Stream content, string extension, string folder, CancellationToken cancellationToken = default)
    {
        string directory = Path.Combine(RootPath(environment), folder);
        Directory.CreateDirectory(directory);
        string name = $"{Guid.NewGuid():N}{extension}";
        await using (var target = File.Create(Path.Combine(directory, name)))
        {
            await content.CopyToAsync(target, cancellationToken);
        }

        return $"{RequestPath}/{folder}/{name}";
    }
}
