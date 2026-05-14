using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace McpManager.Core.Data.Models.Mcp;

[Table("McpNamespaceTools")]
[Index(nameof(McpNamespaceServerId), nameof(McpToolId), IsUnique = true)]
public class McpNamespaceTool
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool IsEnabled { get; set; } = true;

    [MaxLength(255)]
    public string NameOverride { get; set; }

    [MaxLength(2000)]
    public string DescriptionOverride { get; set; }

    public Guid McpNamespaceServerId { get; set; }
    public virtual McpNamespaceServer McpNamespaceServer { get; set; }

    public Guid McpToolId { get; set; }
    public virtual McpTool McpTool { get; set; }
}
