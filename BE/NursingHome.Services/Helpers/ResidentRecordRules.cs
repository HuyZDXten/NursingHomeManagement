using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Residents;

namespace NursingHome.Services.Helpers;

/// <summary>BL-V2: quy tắc hồ sơ lưu trú, viết thành hàm thuần để unit test.</summary>
public static class ResidentRecordRules
{
    public const long MaxDocumentBytes = 10 * 1024 * 1024;

    /// <summary>Tái nhập viện: ngày bắt đầu gói không xa quá 30 ngày tới.</summary>
    public const int MaxReadmitDaysAhead = 30;

    /// <summary>Giấy tờ/hợp đồng: PDF, JPG, PNG ≤ 10 MB, nhận dạng bằng chữ ký file. Trả (đuôi lưu, content-type).</summary>
    public static (string Extension, string ContentType) ValidateDocument(string? fileName, long length, ReadOnlySpan<byte> header)
    {
        if (length <= 0)
        {
            throw new BusinessRuleException("Chưa chọn file.");
        }

        if (length > MaxDocumentBytes)
        {
            throw new BusinessRuleException("File tối đa 10 MB.");
        }

        string extension = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (extension is not (".pdf" or ".jpg" or ".jpeg" or ".png"))
        {
            throw new BusinessRuleException("Chỉ nhận file PDF, JPG hoặc PNG.");
        }

        if (header.Length >= 5 && header[..5].SequenceEqual("%PDF-"u8))
        {
            return (".pdf", "application/pdf");
        }

        return ImageRules.DetectExtension(header) switch
        {
            ".jpg" => (".jpg", "image/jpeg"),
            ".png" => (".png", "image/png"),
            _ => throw new BusinessRuleException("File không phải PDF, JPG hoặc PNG hợp lệ.")
        };
    }

    /// <summary>Tạm vắng: không bắt đầu ở tương lai (quá 5 phút lệch giờ), ngày dự kiến về không trước ngày đi.</summary>
    public static void ValidateLeave(DateTime startUtc, DateOnly? expectedReturnOn, DateTime nowUtc)
    {
        if (startUtc > nowUtc.AddMinutes(5))
        {
            throw new BusinessRuleException("Thời điểm đi không được ở tương lai. Ghi nhận khi cư dân thực sự rời cơ sở.");
        }

        if (expectedReturnOn is { } back && back < FacilityClock.ToLocalDate(startUtc))
        {
            throw new BusinessRuleException("Ngày dự kiến về không được trước ngày đi.");
        }
    }

    public static void ValidateReturn(DateTime startUtc, DateTime returnedUtc, DateTime nowUtc)
    {
        if (returnedUtc < startUtc)
        {
            throw new BusinessRuleException("Thời điểm về không được trước thời điểm đi.");
        }

        if (returnedUtc > nowUtc.AddMinutes(5))
        {
            throw new BusinessRuleException("Thời điểm về không được ở tương lai.");
        }
    }

    /// <summary>Tái nhập: ngày bắt đầu gói không trước ngày ngừng lưu trú lần trước, không quá 30 ngày tới.</summary>
    public static void ValidateReadmitStart(DateOnly startDate, DateOnly dischargedOn, DateOnly today)
    {
        if (startDate < dischargedOn)
        {
            throw new BusinessRuleException("Ngày bắt đầu không được trước ngày ngừng lưu trú lần trước.");
        }

        if (startDate > today.AddDays(MaxReadmitDaysAhead))
        {
            throw new BusinessRuleException($"Ngày bắt đầu tối đa {MaxReadmitDaysAhead} ngày tới.");
        }
    }

    public static void ValidateContract(CreateContractRequest request)
    {
        if (request.StartDate < request.SignedOn.AddDays(-365))
        {
            throw new BusinessRuleException("Ngày bắt đầu cách ngày ký quá xa.");
        }

        if (request.EndDate is { } end && end < request.StartDate)
        {
            throw new BusinessRuleException("Ngày kết thúc không được trước ngày bắt đầu.");
        }

        if (request.MonthlyPrice is { } price && decimal.Round(price, 0) != price)
        {
            throw new BusinessRuleException("Giá tháng phải là số đồng (không có phần lẻ).");
        }
    }

    public static void EnsureWholeVnd(decimal amount, string label)
    {
        if (decimal.Round(amount, 0) != amount)
        {
            throw new BusinessRuleException($"{label} phải là số đồng (không có phần lẻ).");
        }
    }
}
