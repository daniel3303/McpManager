using System.ComponentModel.DataAnnotations;

namespace McpManager.Core.Data.Models.Mcp;

public enum McpTransportType
{
    [Display(Name = "HTTP")]
    Http,

    [Display(Name = "Stdio")]
    Stdio,

    [Display(Name = "SSE")]
    Sse,

    [Display(Name = "OpenAPI")]
    OpenApi,
}
