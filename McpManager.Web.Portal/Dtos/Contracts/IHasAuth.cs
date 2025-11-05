namespace McpManager.Web.Portal.Dtos.Contracts;

/// <summary>
/// Contract for DTOs that contain authentication settings.
/// Used by the shared _AuthForm partial view to render auth fields
/// for both MCP servers and A2A agents.
/// </summary>
public interface IHasAuth {
    AuthDto Auth { get; set; }
}
