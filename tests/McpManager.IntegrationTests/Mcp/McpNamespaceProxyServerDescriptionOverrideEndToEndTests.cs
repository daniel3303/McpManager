using System.Net.Http.Headers;
using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpNamespaceProxyServerDescriptionOverrideEndToEndTests
    : IClassFixture<WebFactoryFixture>
{
    private const string OverrideDescription =
        "Operator-curated description shown to MCP clients in this namespace.";

    private readonly WebFactoryFixture _factory;

    public McpNamespaceProxyServerDescriptionOverrideEndToEndTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task NamespaceMcp_ToolWithDescriptionOverride_AdvertisesOverrideNotUpstreamDesc()
    {
        var ct = TestContext.Current.CancellationToken;
        var (apiKey, slug) = await SeedNamespaceWithDescribedEchoToolAsync();

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

        // Contract: a namespace DescriptionOverride is what the namespace
        // endpoint advertises for that tool, taking precedence over the
        // upstream/synced description. A regression would leak the original
        // upstream text (here the test server's "Echoes ... caller.").
        var tools = await client.ListToolsAsync(cancellationToken: ct);

        var echo = tools.Should().ContainSingle(t => t.Name == "echo").Subject;
        echo.Description.Should().Be(OverrideDescription);
        echo.Description.Should().NotContain("Echoes the supplied message");
    }

    private async Task<(string apiKey, string slug)> SeedNamespaceWithDescribedEchoToolAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var serverManager = sp.GetRequiredService<McpServerManager>();
        var namespaceManager = sp.GetRequiredService<McpNamespaceManager>();
        var nsToolRepo = sp.GetRequiredService<McpNamespaceToolRepository>();
        var apiKeyManager = sp.GetRequiredService<ApiKeyManager>();

        var server = await serverManager.Create(
            new McpServer
            {
                Name = $"nsdesc-srv-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Stdio,
                Command = "dotnet",
                Arguments = [TestStdioServerLocator.DllPath],
            }
        );
        var sync = await serverManager.SyncTools(server);
        sync.Success.Should().BeTrue($"server-level SyncTools precondition: {sync.Error}");

        var slug = $"e2edesc{Guid.NewGuid():N}"[..16];
        var ns = await namespaceManager.Create(
            new McpNamespace { Name = "Desc Override NS", Slug = slug }
        );
        var nsServer = await namespaceManager.AddServer(ns, server);

        var nsTool = await nsToolRepo
            .GetByNamespaceServer(nsServer)
            .Include(t => t.McpTool)
            .SingleAsync();
        await namespaceManager.UpdateToolOverride(nsTool, null, OverrideDescription);

        var apiKey = await apiKeyManager.Create(new ApiKey { Name = $"nsdesc-{Guid.NewGuid():N}" });
        return (apiKey.Key, slug);
    }
}
