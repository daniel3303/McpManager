namespace McpManager.Core.Mcp.Models;

public class ToolExecutionResult
{
    public bool Success { get; set; }
    public string Error { get; set; }
    public List<ToolContent> Content { get; set; } = [];
    public System.Text.Json.JsonElement? StructuredContent { get; set; }
    public bool? IsError { get; set; }
    public long ExecutionTimeMs { get; set; }
}
