using System.Net.Http.Headers;
using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpNamespaceEndpointEndToEndTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespaceEndpointEndToEndTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task PostNamespaceMcp_WithValidApiKey_AggregatesAndCallsToolViaNamespaceSlug()
    {
        var ct = TestContext.Current.CancellationToken;
        var (apiKey, slug) = await SeedNamespaceWithStdioServerAndApiKeyAsync();

        var httpClient = _factory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            apiKey
        );

        // /mcp/ns/{slug} is a distinct route from /mcp — Program.cs picks the
        // namespace proxy handlers when slug is present in route values. The
        // rate-limit partition keys off the slug too. The Stateless=true
        // option lets us drive this over the in-memory TestServer.
        await using var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, $"/mcp/ns/{slug}"),
            },
            httpClient,
            ownsHttpClient: false
        );
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: ct);

        // ListToolsHandler reads from McpNamespaceToolRepository (joined via
        // McpNamespaceServer) — completely different code path from the
        // global proxy. Asserts the echo tool added via the namespace's
        // AddServer flow is visible.
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        tools.Should().Contain(t => t.Name == "echo");

        // CallToolHandler resolves the tool via the namespace tool table,
        // dispatches to the original server via McpServerManager.CallTool,
        // and maps the response. The override-name lookup uses NameOverride
        // ?? McpTool.Name so even without overrides the path is exercised.
        var result = await client.CallToolAsync(
            "echo",
            new Dictionary<string, object> { ["message"] = "ns-hello" },
            cancellationToken: ct
        );

        result.IsError.Should().NotBe(true);
        result
            .Content.OfType<TextContentBlock>()
            .Should()
            .Contain(c => c.Text != null && c.Text.Contains("ns-hello"));
    }

    private async Task<(string apiKey, string slug)> SeedNamespaceWithStdioServerAndApiKeyAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var serverManager = scope.ServiceProvider.GetRequiredService<McpServerManager>();
        var namespaceManager = scope.ServiceProvider.GetRequiredService<McpNamespaceManager>();
        var apiKeyManager = scope.ServiceProvider.GetRequiredService<ApiKeyManager>();

        var server = await serverManager.Create(
            new McpServer
            {
                Name = $"ns-e2e-srv-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Stdio,
                Command = "dotnet",
                Arguments = [TestStdioServerLocator.DllPath],
            }
        );
        var sync = await serverManager.SyncTools(server);
        sync.Success.Should().BeTrue($"server-level SyncTools precondition: {sync.Error}");

        var slug = $"e2ens{Guid.NewGuid():N}"[..16];
        var ns = await namespaceManager.Create(new McpNamespace { Name = "E2E NS", Slug = slug });

        // AddServer attaches the server and runs SyncToolsForServer internally,
        // populating McpNamespaceTool rows — the precondition for the proxy's
        // ListToolsHandler to find tools at the namespace level.
        await namespaceManager.AddServer(ns, server);

        var apiKey = await apiKeyManager.Create(new ApiKey { Name = $"ns-e2e-{Guid.NewGuid():N}" });
        return (apiKey.Key, slug);
    }
}
