using Equibles.Core.AutoWiring;
using Microsoft.Extensions.DependencyInjection;

namespace McpManager.Core.Mcp.Extensions;

public static class McpServiceCollectionExtensions
{
    public static void AddMcp(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AutoWireServicesFrom<McpAssembly>();
    }
}
