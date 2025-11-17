using System.ComponentModel.DataAnnotations;
using McpManager.Core.Data.Models.Mcp;

namespace McpManager.Web.Portal.Dtos.Mcp;

public class McpNamespaceDto {
    [Required]
    [MaxLength(255)]
    [Display(Name = "Name")]
    public string Name { get; set; }

    [Required]
    [MaxLength(255)]
    [RegularExpression(@"^[a-z0-9][a-z0-9-]*$", ErrorMessage = "Slug must contain only lowercase letters, numbers, and hyphens")]
    [Display(Name = "Slug")]
    public string Slug { get; set; }

    [Display(Name = "Description")]
    public string Description { get; set; }

    [Display(Name = "Rate Limit Enabled")]
    public bool RateLimitEnabled { get; set; }

    [Display(Name = "Requests Per Minute")]
    public int RateLimitRequestsPerMinute { get; set; } = 60;

    [Display(Name = "Rate Limit Strategy")]
    public RateLimitStrategy RateLimitStrategy { get; set; } = RateLimitStrategy.PerApiKey;
}
