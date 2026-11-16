using System.Globalization;
using System.Text;

namespace NursingHome.Services.Helpers;

/// <summary>S3-H2: chuẩn hóa chuỗi tiếng Việt để so khớp dị ứng / trùng tên thuốc không phân biệt hoa thường và dấu.</summary>
public static class TextMatch
{
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "";
        }

        string decomposed = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(c == 'đ' ? 'd' : c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
