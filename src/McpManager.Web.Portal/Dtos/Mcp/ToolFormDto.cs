namespace McpManager.Web.Portal.Dtos.Mcp;

public class ToolFormDto
{
    public Guid ToolId { get; set; }
    public string ToolName { get; set; }
    public string Description { get; set; }
    public List<ToolFormFieldDto> Fields { get; set; } = [];
}
