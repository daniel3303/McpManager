using System.ComponentModel.DataAnnotations;

namespace McpManager.Web.Portal.Services.FlashMessage.Contracts;

public enum FlashMessageType
{
    [Display(Name = "Info")]
    Info,

    [Display(Name = "Warning")]
    Warning,

    [Display(Name = "Error")]
    Error,

    [Display(Name = "Success")]
    Success,
}
