namespace McpManager.Core.Mcp.Models;

public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Level { get; set; }
    public string Message { get; set; }
    public Guid? ServerId { get; set; }
    public string ServerName { get; set; }
    public string ToolName { get; set; }
}
