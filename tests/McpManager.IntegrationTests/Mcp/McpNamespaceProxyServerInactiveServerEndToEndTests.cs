using System.Net.Http.Headers;
using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpNamespaceProxyServerInactiveServerEndToEndTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespaceProxyServerInactiveServerEndToEndTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task NamespaceMcp_InactiveNamespaceServer_HidesAllItsToolsAndBlocksCalls()
    {
        var ct = TestContext.Current.CancellationToken;
        var (apiKey, slug) = await SeedNamespaceWithInactiveServerAsync();

        var httpClient = _factory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            apiKey
        );

        await using var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, $"/mcp/ns/{slug}"),
            },
            httpClient,
            ownsHttpClient: false
        );
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: ct);

        // Security/correctness contract: ToggleServer(false) deactivates the
        // server *within the namespace* — every tool it contributed must drop
        // out of service (not listed, not callable). This is the whole-server
        // gate (McpNamespaceServer.IsActive), distinct from the per-tool
        // IsEnabled gate; a regression would re-expose a deactivated server.
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        tools.Should().NotContain(t => t.Name == "echo");

        var act = async () =>
            await client.CallToolAsync(
                "echo",
                new Dictionary<string, object> { ["message"] = "should-not-run" },
                cancellationToken: ct
            );

        var thrown = await act.Should().ThrowAsync<McpException>();
        thrown.Which.Message.Should().Contain("not found in namespace");
    }

    private async Task<(string apiKey, string slug)> SeedNamespaceWithInactiveServerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var serverManager = sp.GetRequiredService<McpServerManager>();
        var namespaceManager = sp.GetRequiredService<McpNamespaceManager>();
        var apiKeyManager = sp.GetRequiredService<ApiKeyManager>();

        var server = await serverManager.Create(
            new McpServer
            {
                Name = $"nsinact-srv-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Stdio,
                Command = "dotnet",
                Arguments = [TestStdioServerLocator.DllPath],
            }
        );
        var sync = await serverManager.SyncTools(server);
        sync.Success.Should().BeTrue($"server-level SyncTools precondition: {sync.Error}");

        var slug = $"e2einact{Guid.NewGuid():N}"[..16];
        var ns = await namespaceManager.Create(
            new McpNamespace { Name = "Inactive Server NS", Slug = slug }
        );
        var nsServer = await namespaceManager.AddServer(ns, server);
        await namespaceManager.ToggleServer(nsServer, false);

        var apiKey = await apiKeyManager.Create(
            new ApiKey { Name = $"nsinact-{Guid.NewGuid():N}" }
        );
        return (apiKey.Key, slug);
    }
}
