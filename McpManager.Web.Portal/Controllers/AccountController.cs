using McpManager.Core.Data.Models.Identity;
using McpManager.Web.Portal.Controllers.Abstract;
using McpManager.Web.Portal.Dtos.Account;
using McpManager.Web.Portal.Services.FlashMessage.Contracts;
using McpManager.Web.Portal.TagHelpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace McpManager.Web.Portal.Controllers;

public class AccountController : BaseController {
    private readonly UserManager<User> _userManager;
    private readonly IFlashMessage _flashMessage;

    public AccountController(UserManager<User> userManager, IFlashMessage flashMessage) {
        _userManager = userManager;
        _flashMessage = flashMessage;
    }

    [HttpGet]
    public async Task<IActionResult> ChangePassword() {
        ViewData["Title"] = "Change Password";
        ViewData["Menu"] = "Account";
        ViewData["Icon"] = HeroIcons.Render("key", size: 5);

        var user = await GetAuthenticatedUser();
        ViewData["User"] = user;

        return View(new ChangePasswordDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto) {
        ViewData["Title"] = "Change Password";
        ViewData["Menu"] = "Account";
        ViewData["Icon"] = HeroIcons.Render("key", size: 5);

        var user = await GetAuthenticatedUser();
        ViewData["User"] = user;

        if (!ModelState.IsValid) {
            return View(dto);
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);

        if (!result.Succeeded) {
            foreach (var error in result.Errors) {
                ModelState.AddModelError(nameof(dto.NewPassword), error.Description);
            }
            return View(dto);
        }

        _flashMessage.Success("Your password has been changed successfully.");
        return RedirectToAction(nameof(ChangePassword));
    }
}
