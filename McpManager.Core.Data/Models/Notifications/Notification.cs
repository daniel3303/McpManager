using System.ComponentModel.DataAnnotations;
using McpManager.Core.Data.Models.Identity;
using Microsoft.EntityFrameworkCore;

namespace McpManager.Core.Data.Models.Notifications;

[Index(nameof(UserId), nameof(CreationTime))]
[Index(nameof(UserId), nameof(IsRead))]
public class Notification {
    public Guid UserId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Title { get; set; }

    [MaxLength(4000)]
    public string Message { get; set; }


    [Required]
    public virtual User User { get; set; }

    public bool IsRead { get; private set; }

    public DateTime? ReadTime { get; private set; }

    [MaxLength(500)]
    public string Url { get; set; }

    public NotificationType Type { get; set; } = NotificationType.Info;

    [MaxLength(500)]
    public string Icon { get; set; }

    public DateTime CreationTime { get; set; } = DateTime.UtcNow;

    public void MarkAsRead() {
        if (IsRead) return;
        IsRead = true;
        ReadTime = DateTime.UtcNow;
    }
}
