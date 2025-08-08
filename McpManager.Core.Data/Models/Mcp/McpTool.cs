using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace McpManager.Core.Data.Models.Mcp;

[Table("McpTools")]
[Index(nameof(McpServerId), nameof(Name), IsUnique = true)]
public class McpTool {
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    public string Name { get; set; }

    [MaxLength(2000)]
    public string Description { get; set; }

    [MaxLength(8000)]
    public string InputSchema { get; set; }

    [MaxLength(2000)]
    public string CustomDescription { get; set; }

    public string CustomInputSchema { get; set; }

    public string Metadata { get; set; }

    public long TotalRequests { get; set; }

    public DateTime CreationTime { get; set; } = DateTime.UtcNow;

    public virtual McpServer McpServer { get; set; }
    public Guid McpServerId { get; set; }
}
