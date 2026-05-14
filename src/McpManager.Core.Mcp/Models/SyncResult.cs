namespace McpManager.Core.Mcp.Models;

public class SyncResult
{
    public bool Success { get; set; }
    public int ToolsAdded { get; set; }
    public int ToolsUpdated { get; set; }
    public int ToolsRemoved { get; set; }
    public string Error { get; set; }
}
