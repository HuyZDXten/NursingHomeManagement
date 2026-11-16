using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.Services.Implementations;

/// <summary>BL-V1 (chép từ ASS2): lưu ảnh lên Cloudinary, thư mục "nursinghome/{folder}". Chỉ dùng khi đủ 3 khóa.</summary>
public class CloudinaryImageStorage : IImageStorage
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryImageStorage> _logger;

    public CloudinaryImageStorage(IOptions<CloudinaryOptions> options, ILogger<CloudinaryImageStorage> logger)
    {
        var o = options.Value;
        _cloudinary = new Cloudinary(new Account(o.CloudName.Trim(), o.ApiKey.Trim(), o.ApiSecret.Trim()));
        _cloudinary.Api.Secure = true;
        _logger = logger;
    }

    public async Task<string> SaveAsync(Stream content, string extension, string folder, CancellationToken cancellationToken = default)
    {
        var result = await _cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription($"{Guid.NewGuid():N}{extension}", content),
            Folder = $"nursinghome/{folder}",
            UseFilename = false,
            UniqueFilename = true,
            Overwrite = false
        }, cancellationToken);

        if (result.Error is not null || result.SecureUrl is null)
        {
            _logger.LogWarning("Cloudinary từ chối ảnh: {Error}", result.Error?.Message);
            throw new ServiceUnavailableException("Không tải được ảnh lên kho ảnh. Vui lòng thử lại sau.");
        }

        return result.SecureUrl.ToString();
    }
}
