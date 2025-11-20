namespace McpManager.Web.Portal.Dtos.Mcp;

public class McpRequestSearchDto : IFilterDto {
    public Guid? ServerId { get; set; }
    public Guid? ToolId { get; set; }
    public bool? Success { get; set; }
}
