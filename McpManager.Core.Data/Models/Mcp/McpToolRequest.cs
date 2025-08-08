using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace McpManager.Core.Data.Models.Mcp;

[Table("McpToolRequests")]
public class McpToolRequest {
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid McpToolId { get; set; }
    public virtual McpTool McpTool { get; set; }

    [MaxLength(255)]
    public string ApiKeyName { get; set; }

    public Guid? McpNamespaceId { get; set; }
    public virtual McpNamespace McpNamespace { get; set; }

    public string Parameters { get; set; }

    public string Response { get; set; }

    public bool Success { get; set; }

    public long ExecutionTimeMs { get; set; }

    public DateTime CreationTime { get; set; } = DateTime.UtcNow;
}
