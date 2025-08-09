using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace McpManager.Core.Data.Models.Mcp;

[Index(nameof(McpServerId), nameof(CreationTime))]
public class McpServerLog {
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid McpServerId { get; set; }

    public virtual McpServer McpServer { get; set; }

    public McpServerLogLevel Level { get; set; }

    [Required]
    [MaxLength(4000)]
    public string Message { get; set; }

    public DateTime CreationTime { get; set; } = DateTime.UtcNow;
}
