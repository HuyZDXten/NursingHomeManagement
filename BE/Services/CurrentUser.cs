using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NursingHomeManagement.Data;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.Services;

public sealed class CurrentUser(IHttpContextAccessor accessor, ApplicationDbContext database)
{
    public ClaimsPrincipal Principal => accessor.HttpContext?.User ?? new ClaimsPrincipal();
    public string Id => Principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();

    public bool IsInRole(string role) => Principal.IsInRole(role);
    public async Task RequireRoleAsync(params string[] roles)
    {
        if (!roles.Any(IsInRole) || !await database.Users.AnyAsync(user => user.Id == Id && user.IsActive))
        {
            throw new UnauthorizedAccessException();
        }
    }
}
