using System.ComponentModel.DataAnnotations;

namespace McpManager.Core.Data.Models.Mcp;

public enum RateLimitStrategy {
    [Display(Name = "Per API Key")] PerApiKey,
    [Display(Name = "Per IP")] PerIp,
    [Display(Name = "Global")] Global
}
