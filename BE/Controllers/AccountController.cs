using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NursingHomeManagement.Models;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Controllers;

[Authorize]
public sealed class AccountController(
    SignInManager<ApplicationUser> signIn,
    UserManager<ApplicationUser> users) : WorkflowController
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await users.FindByEmailAsync(model.Email);
        if (user is not null && user.IsActive)
        {
            var result = await signIn.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                if (Url.IsLocalUrl(model.ReturnUrl))
                {
                    return LocalRedirect(model.ReturnUrl!);
                }

                if (await users.IsInRoleAsync(user, Roles.Family))
                {
                    return RedirectToAction("Index", "Family");
                }

                if (await users.IsInRoleAsync(user, Roles.Caregiver))
                {
                    return RedirectToAction("Index", "Caregiver");
                }

                return RedirectToAction("Index", "Home");
            }
        }

        ModelState.AddModelError(string.Empty, "Đăng nhập không thành công. Kiểm tra thông tin hoặc thử lại sau.");
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous, HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    [HttpGet]
    public IActionResult ChangePassword() => View("Form", new ChangePasswordForm());
    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordForm form)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", form);
        }

        var user = await users.GetUserAsync(User) ?? throw new UnauthorizedAccessException();
        var result = await users.ChangePasswordAsync(user, form.CurrentPassword, form.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View("Form", form);
        }

        await signIn.RefreshSignInAsync(user);
        TempData["Success"] = "Đã đổi mật khẩu.";
        return RedirectToAction(nameof(ChangePassword));
    }
}
