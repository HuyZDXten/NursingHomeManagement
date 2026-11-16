using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.Services.Helpers;

/// <summary>
/// BL-V1: kiểm tra ảnh tải lên. Định dạng xác định bằng chữ ký đầu file (magic bytes), không tin đuôi tên file
/// hay Content-Type do trình duyệt gửi. Đuôi lưu = đuôi theo chữ ký, nên file .png chứa HTML không bao giờ được phục vụ.
/// </summary>
public static class ImageRules
{
    public const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>Số byte đầu cần đọc để nhận dạng.</summary>
    public const int HeaderLength = 12;

    public static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    /// <summary>Thư mục lưu ảnh. "care" để Huy dùng cho ảnh đính kèm ghi chép chăm sóc (UC-60).</summary>
    public const string AvatarFolder = "avatars";
    public const string ResidentFolder = "residents";
    public const string GeneralFolder = "images";
    public const string CareFolder = "care";

    public static readonly string[] Folders = [AvatarFolder, ResidentFolder, GeneralFolder, CareFolder];

    /// <summary>".jpg" | ".png" | ".webp" theo chữ ký, null nếu không phải 3 định dạng này.</summary>
    public static string? DetectExtension(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return ".jpg";
        }

        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ".png";
        }

        // RIFF....WEBP
        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return ".webp";
        }

        return null;
    }

    /// <summary>Trả đuôi lưu (theo chữ ký) hoặc ném BusinessRuleException với thông báo tiếng Việt.</summary>
    public static string Validate(string? fileName, long length, ReadOnlySpan<byte> header)
    {
        if (length <= 0)
        {
            throw new BusinessRuleException("Chưa chọn ảnh.");
        }

        if (length > MaxBytes)
        {
            throw new BusinessRuleException("Ảnh tối đa 5 MB.");
        }

        string extension = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            throw new BusinessRuleException("Chỉ nhận ảnh JPG, PNG hoặc WebP.");
        }

        return DetectExtension(header) ?? throw new BusinessRuleException("File không phải ảnh JPG, PNG hoặc WebP hợp lệ.");
    }
}
