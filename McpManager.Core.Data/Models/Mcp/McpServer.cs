using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using McpManager.Core.Data.Models.Contracts;
using McpManager.Core.Data.Models.Authentication;

namespace McpManager.Core.Data.Models.Mcp;

[Table("McpServers")]
public class McpServer : IActivable {
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool IsActive { get; set; } = true;

    [Required]
    [MaxLength(255)]
    public string Name { get; set; }

    [MaxLength(1000)]
    public string Description { get; set; }

    // Transport
    public McpTransportType TransportType { get; set; } = McpTransportType.Http;

    // HTTP transport fields
    [MaxLength(2000)]
    public string Uri { get; set; }

    [Required]
    public Auth Auth { get; set; } = new();

    public Dictionary<string, string> CustomHeaders { get; set; } = [];

    // Stdio transport fields
    [MaxLength(500)]
    public string Command { get; set; }

    public List<string> Arguments { get; set; } = [];

    public Dictionary<string, string> EnvironmentVariables { get; set; } = [];

    // OpenAPI transport fields
    public string OpenApiSpecification { get; set; }

    // Metadata
    public DateTime? LastSyncTime { get; set; }

    [MaxLength(2000)]
    public string LastError { get; set; }

    public DateTime CreationTime { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual List<McpTool> Tools { get; set; } = [];
}
