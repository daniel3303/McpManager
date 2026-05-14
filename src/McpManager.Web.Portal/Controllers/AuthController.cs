using McpManager.Core.Data.Models.Identity;
using McpManager.Web.Portal.Controllers.Abstract;
using McpManager.Web.Portal.Dtos.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace McpManager.Web.Portal.Controllers;

[AllowAnonymous]
public class AuthController : BaseController
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;

    public AuthController(SignInManager<User> signInManager, UserManager<User> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Login(string returnUrl, string error)
    {
        await _signInManager.SignOutAsync();
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["Error"] = error;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginDto loginDto)
    {
        if (!ModelState.IsValid)
        {
            return View(loginDto);
        }

        var user = await _userManager.FindByNameAsync(loginDto.Email);
        if (user?.IsActive ?? false)
        {
            var result = await _signInManager.PasswordSignInAsync(
                loginDto.Email,
                loginDto.Password,
                true,
                false
            );

            if (result.Succeeded)
            {
                return RedirectToAction("Index", "Home");
            }
        }
        ViewData["InvalidLogin"] = "Invalid credentials or inactive account.";
        return View(loginDto);
    }

    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return SignOut(
            new AuthenticationProperties() { RedirectUri = Url.Action("Index", "Home") }
        );
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "Access denied";
        ViewData["Text"] = "You do not have access to the requested page.";
        return View();
    }

    [HttpGet]
    public IActionResult LockedOut()
    {
        ViewData["Title"] = "Account locked";
        ViewData["Text"] = "Your account is locked.";
        return View();
    }
}
