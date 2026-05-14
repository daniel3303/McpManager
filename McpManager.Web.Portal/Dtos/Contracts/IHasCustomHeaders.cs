namespace McpManager.Web.Portal.Dtos.Contracts;

/// <summary>
/// Contract for DTOs that contain custom HTTP headers.
/// Used by the shared _CustomHeadersForm partial view to render header inputs
/// for both MCP servers and A2A agents.
/// </summary>
public interface IHasCustomHeaders
{
    List<CustomHeaderDto> CustomHeaders { get; set; }
}
