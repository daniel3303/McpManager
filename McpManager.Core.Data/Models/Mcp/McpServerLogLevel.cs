using System.ComponentModel.DataAnnotations;

namespace McpManager.Core.Data.Models.Mcp;

public enum McpServerLogLevel
{
    [Display(Name = "Info")]
    Info = 0,

    [Display(Name = "Warning")]
    Warning = 1,

    [Display(Name = "Error")]
    Error = 2,
}
