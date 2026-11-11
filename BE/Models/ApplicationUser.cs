using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace NursingHomeManagement.Models;

public class ApplicationUser : IdentityUser
{
    [Required, MaxLength(150)]
    public string FullName { get; set; } = "";

    public bool IsActive { get; set; } = true;
}
