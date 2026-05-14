using McpManager.Core.Identity;
using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using McpManager.Web.Portal.Controllers.Abstract;
using McpManager.Web.Portal.Services.FlashMessage.Contracts;
using McpManager.Web.Portal.TagHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace McpManager.Web.Portal.Controllers;

public class NotificationsController : BaseController
{
    private readonly NotificationRepository _notificationRepository;
    private readonly NotificationManager _notificationManager;
    private readonly IFlashMessage _flashMessage;

    public NotificationsController(
        NotificationRepository notificationRepository,
        NotificationManager notificationManager,
        IFlashMessage flashMessage
    )
    {
        _notificationRepository = notificationRepository;
        _notificationManager = notificationManager;
        _flashMessage = flashMessage;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Notifications";
        ViewData["Menu"] = "Notifications";
        ViewData["Icon"] = HeroIcons.Render("bell", size: 5);

        var user = await GetAuthenticatedUser();
        var query = _notificationRepository.GetByUser(user).OrderByDescending(n => n.CreationTime);

        return View(query);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Show(Guid id)
    {
        ViewData["Title"] = "Notification Details";
        ViewData["Menu"] = "Notifications";
        ViewData["Icon"] = HeroIcons.Render("bell", size: 5);

        var user = await GetAuthenticatedUser();
        var notification = await _notificationRepository
            .GetByUser(user)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (notification == null)
        {
            _flashMessage.Error("Notification not found.");
            return RedirectToAction(nameof(Index));
        }

        // Mark as read
        if (!notification.IsRead)
        {
            await _notificationManager.MarkAsRead(notification);
        }

        // If notification has a URL, redirect to it
        if (!string.IsNullOrWhiteSpace(notification.Url))
        {
            return Redirect(notification.Url);
        }

        return View(notification);
    }

    [HttpPost("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var user = await GetAuthenticatedUser();
        var notification = await _notificationRepository
            .GetByUser(user)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (notification != null)
        {
            await _notificationManager.MarkAsRead(notification);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var user = await GetAuthenticatedUser();
        await _notificationManager.MarkAllAsRead(user);
        _flashMessage.Success("All notifications marked as read.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await GetAuthenticatedUser();
        var notification = await _notificationRepository
            .GetByUser(user)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (notification == null)
        {
            _flashMessage.Error("Notification not found.");
            return RedirectToAction(nameof(Index));
        }

        await _notificationManager.Delete(notification);
        _flashMessage.Success("Notification deleted.");
        return RedirectToAction(nameof(Index));
    }

    #region API

    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        var user = await GetAuthenticatedUser();
        var count = await _notificationRepository.GetUnreadByUser(user).CountAsync();

        return Ok(new { Count = count });
    }

    [HttpGet]
    public async Task<IActionResult> Recent()
    {
        var user = await GetAuthenticatedUser();
        var notifications = await _notificationRepository
            .GetByUser(user)
            .OrderByDescending(n => n.CreationTime)
            .Take(5)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Message,
                n.Type,
                n.Icon,
                n.IsRead,
                n.Url,
                n.CreationTime,
            })
            .ToListAsync();

        return Ok(notifications);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiMarkAsRead(Guid id)
    {
        var user = await GetAuthenticatedUser();
        var notification = await _notificationRepository
            .GetByUser(user)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (notification == null)
        {
            return NotFound();
        }

        await _notificationManager.MarkAsRead(notification);
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiMarkAllAsRead()
    {
        var user = await GetAuthenticatedUser();
        await _notificationManager.MarkAllAsRead(user);
        return Ok();
    }

    #endregion
}
