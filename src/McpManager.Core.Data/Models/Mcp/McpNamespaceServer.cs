using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace McpManager.Core.Data.Models.Mcp;

[Table("McpNamespaceServers")]
[Index(nameof(McpNamespaceId), nameof(McpServerId), IsUnique = true)]
public class McpNamespaceServer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool IsActive { get; set; } = true;

    public Guid McpNamespaceId { get; set; }
    public virtual McpNamespace McpNamespace { get; set; }

    public Guid McpServerId { get; set; }
    public virtual McpServer McpServer { get; set; }

    // Navigation
    public virtual List<McpNamespaceTool> Tools { get; set; } = [];
}
