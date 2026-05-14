namespace McpManager.Web.Portal.Dtos.Mcp;

public class McpToolEditDto
{
    public string CustomDescription { get; set; }
    public List<McpToolArgumentDto> Arguments { get; set; } = [];
}
