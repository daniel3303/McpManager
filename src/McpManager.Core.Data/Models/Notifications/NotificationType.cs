using System.ComponentModel.DataAnnotations;

namespace McpManager.Core.Data.Models.Notifications;

public enum NotificationType
{
    [Display(Name = "Information")]
    Info = 0,

    [Display(Name = "Success")]
    Success = 1,

    [Display(Name = "Warning")]
    Warning = 2,

    [Display(Name = "Error")]
    Error = 3,

    [Display(Name = "System")]
    System = 4,
}
