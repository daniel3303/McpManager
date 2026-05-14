using McpManager.Core.Data.Models;
using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using McpManager.Web.Portal.Controllers.Abstract;
using McpManager.Web.Portal.Services.FlashMessage.Contracts;
using McpManager.Web.Portal.TagHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace McpManager.Web.Portal.Controllers;

[Authorize(Policy = "Admin")]
public class AdminSettingsController : BaseController
{
    private readonly AppSettingsRepository _settingsRepository;
    private readonly IFlashMessage _flashMessage;

    public AdminSettingsController(
        AppSettingsRepository settingsRepository,
        IFlashMessage flashMessage
    )
    {
        _settingsRepository = settingsRepository;
        _flashMessage = flashMessage;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Settings";
        ViewData["Menu"] = "AdminSettings";
        ViewData["Icon"] = HeroIcons.Render("cog-6-tooth", size: 5);

        var settings = await _settingsRepository.Get(1) ?? new AppSettings();
        return View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(AppSettings dto)
    {
        ViewData["Title"] = "Settings";
        ViewData["Menu"] = "AdminSettings";
        ViewData["Icon"] = HeroIcons.Render("cog-6-tooth", size: 5);

        if (!ModelState.IsValid)
        {
            return View(dto);
        }

        var settings = await _settingsRepository.Get(1);
        if (settings == null)
        {
            settings = new AppSettings { Id = 1 };
            _settingsRepository.Add(settings);
        }

        settings.McpConnectionTimeoutSeconds = dto.McpConnectionTimeoutSeconds;
        settings.McpRetryAttempts = dto.McpRetryAttempts;

        await _settingsRepository.SaveChanges();

        _flashMessage.Success("Settings saved successfully.");
        return RedirectToAction(nameof(Index));
    }
}
