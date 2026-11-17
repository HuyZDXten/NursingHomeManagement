using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.Services.Helpers;
using NursingHome.Services.Options;

namespace NursingHome.Services.Implementations;

/// <summary>
/// UC-129: nguồn <see cref="FacilityOptions"/> duy nhất của cả API = appsettings ghi đè bởi bảng SystemSettings.
/// Đăng ký thay cho IOptions&lt;FacilityOptions&gt; mặc định nên mọi service đang dùng cấu hình cơ sở tự nhận giá trị mới
/// (đọc <c>.Value</c> ở mỗi request, không cần khởi động lại). Thay cả đối tượng một lần nên người đọc không bao giờ thấy trạng thái nửa vời.
/// </summary>
public sealed class FacilitySettingsStore : IOptions<FacilityOptions>
{
    private readonly FacilityOptions _baseline;
    private volatile FacilityOptions _current;

    public FacilitySettingsStore(IConfiguration configuration)
    {
        _baseline = configuration.GetSection(FacilityOptions.SectionName).Get<FacilityOptions>() ?? new FacilityOptions();
        _current = SettingsCatalog.Clone(_baseline);
    }

    public FacilityOptions Value => _current;

    /// <summary>Giá trị trong appsettings (bản sao), dùng để hiển thị "mặc định" và nút bỏ ghi đè.</summary>
    public FacilityOptions Baseline => SettingsCatalog.Clone(_baseline);

    /// <summary>Dựng lại từ appsettings + các ghi đè. Dòng có khóa lạ hoặc giá trị hỏng (sửa tay trong DB) bị bỏ qua, không làm API hỏng.</summary>
    public void Apply(IEnumerable<SystemSetting> overrides)
    {
        var next = SettingsCatalog.Clone(_baseline);
        foreach (var row in overrides)
        {
            var definition = SettingsCatalog.Find(row.Key);
            if (definition is null)
            {
                continue;
            }

            try
            {
                SettingsCatalog.Write(next, row.Key, SettingsCatalog.Normalize(definition, row.Value));
            }
            catch (BusinessRuleException)
            {
                // giữ giá trị appsettings cho khóa này
            }
        }

        try
        {
            SettingsCatalog.ValidateConsistency(next);
        }
        catch (BusinessRuleException)
        {
            next = SettingsCatalog.Clone(_baseline);
        }

        _current = next;
    }
}
