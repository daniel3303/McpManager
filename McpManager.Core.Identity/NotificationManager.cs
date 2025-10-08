using McpManager.Core.Data.Models.Identity;
using McpManager.Core.Data.Models.Notifications;
using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using Equibles.Core.AutoWiring;
using Microsoft.EntityFrameworkCore;

namespace McpManager.Core.Identity;

[Service]
public class NotificationManager {
    private readonly NotificationRepository _notificationRepository;
    private readonly UserRepository _userRepository;

    public NotificationManager(
        NotificationRepository notificationRepository,
        UserRepository userRepository
    ) {
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
    }

    public async Task<Notification> Create(
        User user,
        string title,
        string message = null,
        NotificationType type = NotificationType.Info,
        string url = null,
        string icon = null
    ) {
        var notification = new Notification {
            User = user,
            Title = title,
            Message = message,
            Type = type,
            Url = url,
            Icon = icon ?? GetDefaultIcon(type)
        };

        _notificationRepository.Add(notification);
        await _notificationRepository.SaveChanges();

        return notification;
    }

    public async Task CreateForAllUsers(
        string title,
        string message = null,
        NotificationType type = NotificationType.Info,
        string url = null,
        string icon = null
    ) {
        var users = await _userRepository.GetAll()
            .Where(u => u.IsActive)
            .ToListAsync();

        foreach (var user in users) {
            var notification = new Notification {
                User = user,
                Title = title,
                Message = message,
                Type = type,
                Url = url,
                Icon = icon ?? GetDefaultIcon(type)
            };
            _notificationRepository.Add(notification);
        }

        await _notificationRepository.SaveChanges();
    }

    public async Task MarkAsRead(Notification notification) {
        notification.MarkAsRead();
        await _notificationRepository.SaveChanges();
    }

    public async Task MarkAllAsRead(User user) {
        var unreadNotifications = await _notificationRepository
            .GetUnreadByUser(user)
            .ToListAsync();

        foreach (var notification in unreadNotifications) {
            notification.MarkAsRead();
        }

        await _notificationRepository.SaveChanges();
    }

    public async Task Delete(Notification notification) {
        _notificationRepository.Remove(notification);
        await _notificationRepository.SaveChanges();
    }

    private static string GetDefaultIcon(NotificationType type) {
        return type switch {
            NotificationType.Success => "check-circle",
            NotificationType.Warning => "exclamation-triangle",
            NotificationType.Error => "x-circle",
            NotificationType.System => "cog-6-tooth",
            _ => "information-circle"
        };
    }
}
