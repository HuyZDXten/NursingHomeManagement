using System.ComponentModel.DataAnnotations;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.ViewModels;

public sealed class BedAssignmentForm
{
    [Range(1, int.MaxValue)]
    [Display(Name = "Cư dân")]
    public int ResidentId
    {
        get; set;
    }

    [Range(1, int.MaxValue)]
    [Display(Name = "Giường mới")]
    public int BedId
    {
        get; set;
    }
}
