using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using McpManager.Core.Data.Models.Contracts;
using McpManager.Core.Data.Models.Mcp;

namespace McpManager.Core.Data.Models.ApiKeys;

[Table("ApiKeys")]
public class ApiKey : IActivable {
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    public string Name { get; set; }

    [Required]
    [MaxLength(500)]
    public string Key { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreationTime { get; set; } = DateTime.UtcNow;

    // Namespace scoping — empty means access all namespaces
    public virtual List<McpNamespace> AllowedNamespaces { get; set; } = [];
}
