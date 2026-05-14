using McpManager.Web.Portal.Authentication;
using ModelContextProtocol.AspNetCore;

namespace McpManager.Web.Portal.Mcp;

public static class McpEndpointExtensions
{
    public static IEndpointRouteBuilder MapMcpEndpoint(this IEndpointRouteBuilder endpoints)
    {
        // Global proxy endpoint (backward compatible)
        endpoints
            .MapMcp("/mcp")
            .RequireAuthorization(policy =>
                policy
                    .AddAuthenticationSchemes(ApiKeyAuthHandler.SchemeName)
                    .RequireAuthenticatedUser()
            )
            .ExcludeFromDescription();

        return endpoints;
    }

    public static IEndpointRouteBuilder MapMcpNamespaceEndpoint(
        this IEndpointRouteBuilder endpoints
    )
    {
        // Per-namespace proxy endpoint
        endpoints
            .MapMcp("/mcp/ns/{slug}")
            .RequireAuthorization(policy =>
                policy
                    .AddAuthenticationSchemes(ApiKeyAuthHandler.SchemeName)
                    .RequireAuthenticatedUser()
            )
            .RequireRateLimiting("NamespaceRateLimit")
            .ExcludeFromDescription();

        return endpoints;
    }
}
