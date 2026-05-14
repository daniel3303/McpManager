namespace McpManager.Web.Portal.Dtos.Mcp;

public class ToolFormFieldDto
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string Description { get; set; }
    public bool Required { get; set; }
    public string Default { get; set; }
    public List<string> EnumValues { get; set; } = [];
    public int? Minimum { get; set; }
    public int? Maximum { get; set; }
}
