namespace McpManager.Web.Portal.Dtos.Mcp;

public class ExecuteToolDto {
    public Guid ServerId { get; set; }
    public string ToolName { get; set; }
    public Dictionary<string, object> Arguments { get; set; } = new();
}
