using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using McpManager.Web.Portal.Mcp;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;
using DataMcpServer = McpManager.Core.Data.Models.Mcp.McpServer;

namespace McpManager.IntegrationTests.Mcp;

public class McpProxyServerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpProxyServerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task ListToolsHandler_AfterSyncingStdioUpstream_AggregatesTheUpstreamTool()
    {
        using var scope = _factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<McpServerManager>();
        var sut = scope.ServiceProvider.GetRequiredService<McpProxyServer>();
        var ct = TestContext.Current.CancellationToken;

        var server = await manager.Create(
            new DataMcpServer
            {
                Name = $"proxy-list-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Stdio,
                Command = "dotnet",
                Arguments = [TestStdioServerLocator.DllPath],
            }
        );
        var sync = await manager.SyncTools(server);
        sync.Success.Should().BeTrue($"SyncTools is the precondition: {sync.Error}");

        // ListToolsHandler is the /mcp endpoint's tool-discovery surface;
        // the handler only reads from the repository, so request can be null.
        // A regression that filtered out IsActive-true servers (or dropped
        // the projection to ModelContextProtocol Tool objects) would surface
        // here.
        var result = await sut.ListToolsHandler(default, ct);

        result.Should().NotBeNull();
        result.Tools.Should().Contain(t => t.Name == "echo");
    }

    // CallToolHandler's failure-mode tests are not reachable without
    // constructing a real ModelContextProtocol.Server.McpServer for the
    // RequestContext ctor — its argument is null-checked. Exercising
    // CallToolHandler end-to-end belongs in a future PR that drives the
    // /mcp endpoint over HTTP with the official client SDK.
}
