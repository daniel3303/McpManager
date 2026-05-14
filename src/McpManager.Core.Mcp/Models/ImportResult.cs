namespace McpManager.Core.Mcp.Models;

public class ImportResult
{
    public bool Success { get; set; }
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public List<string> Messages { get; set; } = [];
}
