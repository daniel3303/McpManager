using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpNamespaceManagerSyncToolsForAllNamespacesTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespaceManagerSyncToolsForAllNamespacesTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task SyncToolsForAllNamespaces_AfterServerToolRemoved_PrunesOrphanedNamespaceTool()
    {
        var ct = TestContext.Current.CancellationToken;

        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var serverManager = sp.GetRequiredService<McpServerManager>();
        var nsManager = sp.GetRequiredService<McpNamespaceManager>();
        var toolRepo = sp.GetRequiredService<McpToolRepository>();
        var nsToolRepo = sp.GetRequiredService<McpNamespaceToolRepository>();

        var server = await serverManager.Create(
            new McpServer
            {
                Name = $"nsm-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Http,
                Uri = "https://upstream.invalid/mcp",
            }
        );
        var tool = toolRepo.Add(new McpTool { Name = "doomed", McpServerId = server.Id });
        await toolRepo.SaveChanges();

        var slug = "ns-" + Guid.NewGuid().ToString("n")[..12];
        var ns = await nsManager.Create(new McpNamespace { Name = slug, Slug = slug });
        // AddServer also runs SyncToolsForServer, creating the namespace tool.
        var nsServer = await nsManager.AddServer(ns, server);
        (await nsToolRepo.GetByNamespaceServer(nsServer).CountAsync(ct))
            .Should()
            .Be(1, "AddServer should have created the namespace tool");

        // Remove the tool from the server, then sync every namespace that uses
        // it. SyncToolsForAllNamespaces (the nsServers foreach) and the
        // prune branch in SyncToolsForServer (toRemove.Count > 0 -> Remove)
        // were both zero-hit. A regression skipping the loop or the prune
        // would leave namespaces exposing a tool the server no longer has.
        toolRepo.Remove(tool);
        await toolRepo.SaveChanges();

        await nsManager.SyncToolsForAllNamespaces(server);

        (await nsToolRepo.GetByNamespaceServer(nsServer).CountAsync(ct))
            .Should()
            .Be(0, "the orphaned namespace tool must be pruned across all namespaces");
    }
}
