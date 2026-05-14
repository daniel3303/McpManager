using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Data.Models.Contracts;
using Microsoft.EntityFrameworkCore;

namespace McpManager.Core.Data.Models.Mcp;

[Table("McpNamespaces")]
[Index(nameof(Slug), IsUnique = true)]
public class McpNamespace : IActivable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool IsActive { get; set; } = true;

    [Required]
    [MaxLength(255)]
    public string Name { get; set; }

    [Required]
    [MaxLength(255)]
    [RegularExpression(@"^[a-z0-9][a-z0-9-]*$")]
    public string Slug { get; set; }

    [MaxLength(1000)]
    public string Description { get; set; }

    // Rate limiting
    public bool RateLimitEnabled { get; set; }
    public int RateLimitRequestsPerMinute { get; set; } = 60;
    public RateLimitStrategy RateLimitStrategy { get; set; } = RateLimitStrategy.PerApiKey;

    public DateTime CreationTime { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual List<McpNamespaceServer> Servers { get; set; } = [];
    public virtual List<ApiKey> ApiKeys { get; set; } = [];
}
