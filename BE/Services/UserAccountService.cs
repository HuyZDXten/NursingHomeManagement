using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NursingHomeManagement.Data;
using NursingHomeManagement.Models;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Services;

public sealed class UserAccountService(
    ApplicationDbContext database,
    UserManager<ApplicationUser> users,
    CurrentUser currentUser)
{
    public async Task<PagedList<ApplicationUser>> ListAsync(string? search, int page)
    {
        await currentUser.RequireRoleAsync(Roles.Admin);
        var query = database.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(user => user.FullName.Contains(search) || user.Email!.Contains(search));
        }

        return await PagedList<ApplicationUser>.CreateAsync(query.OrderBy(user => user.Email), page);
    }

    public async Task<UserEditForm> GetAsync(string id)
    {
        await currentUser.RequireRoleAsync(Roles.Admin);
        var user = await users.FindByIdAsync(id) ?? throw new KeyNotFoundException();
        var roles = await users.GetRolesAsync(user);
        return new UserEditForm
        {
            FullName = user.FullName,
            IsActive = user.IsActive,
            Role = roles.Single()
        };
    }

    public async Task CreateAsync(UserForm form)
    {
        await currentUser.RequireRoleAsync(Roles.Admin);
        ValidateRole(form.Role);
        await using var transaction = await database.Database.BeginTransactionAsync();
        var user = new ApplicationUser
        {
            FullName = form.FullName.Trim(),
            UserName = form.Email.Trim(),
            Email = form.Email.Trim(),
            EmailConfirmed = true
        };
        Check(await users.CreateAsync(user, form.Password));
        Check(await users.AddToRoleAsync(user, form.Role));
        await transaction.CommitAsync();
    }

    public async Task EditAsync(string id, UserEditForm form)
    {
        await currentUser.RequireRoleAsync(Roles.Admin);
        ValidateRole(form.Role);
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var user = await users.FindByIdAsync(id) ?? throw new KeyNotFoundException();
        var roles = await users.GetRolesAsync(user);
        bool roleChanged = !roles.Contains(form.Role);
        if (id == currentUser.Id && (roleChanged || !form.IsActive))
        {
            throw new BusinessException("Không tự khóa hoặc thay đổi vai trò của tài khoản đang đăng nhập.");
        }

        if (roleChanged && await database.ResidentFamilyLinks.AnyAsync(link => link.FamilyUserId == id))
        {
            throw new BusinessException("Hủy liên kết gia đình trước khi đổi vai trò.");
        }

        if ((roleChanged || !form.IsActive) && await database.CareTasks.AnyAsync(task => task.AssignedToId == id && (task.Status == CareTaskStatus.Pending || task.Status == CareTaskStatus.InProgress)))
        {
            throw new BusinessException("Hoàn thành hoặc hủy các công việc đang được giao trước khi đổi vai trò/khóa tài khoản.");
        }

        user.FullName = form.FullName.Trim();
        user.IsActive = form.IsActive;
        Check(await users.UpdateAsync(user));
        if (roleChanged)
        {
            Check(await users.RemoveFromRolesAsync(user, roles));
            Check(await users.AddToRoleAsync(user, form.Role));
        }

        Check(await users.UpdateSecurityStampAsync(user));
        await transaction.CommitAsync();
    }

    private static void ValidateRole(string role)
    {
        if (!Roles.All.Contains(role))
        {
            throw new BusinessException("Vai trò không hợp lệ.");
        }
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new BusinessException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }
    }
}
