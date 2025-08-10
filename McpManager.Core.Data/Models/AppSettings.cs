using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace McpManager.Core.Data.Models;

[Table("AppSettings")]
public class AppSettings {
    [Key]
    public int Id { get; set; } = 1;

    public int McpConnectionTimeoutSeconds { get; set; } = 120;
    public int McpRetryAttempts { get; set; } = 3;
}
