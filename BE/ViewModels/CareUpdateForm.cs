using System.ComponentModel.DataAnnotations;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed class CareUpdateForm
{
    [EnumDataType(typeof(CareTaskStatus))]
    [Display(Name = "Trạng thái")]
    public CareTaskStatus Status
    {
        get; set;
    }

    [Required, StringLength(2000)]
    [Display(Name = "Kết quả / lý do")]
    public string Result { get; set; } = "";

    [Display(Name = "Cho phép chia sẻ với gia đình")]
    public bool ShareWithFamily
    {
        get; set;
    }

    [Required]
    [Display(Name = "Phiên bản")]
    public string RowVersion { get; set; } = "";
}
