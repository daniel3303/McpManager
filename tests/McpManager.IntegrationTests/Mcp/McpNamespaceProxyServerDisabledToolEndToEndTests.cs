using System.Net.Http.Headers;
using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpNamespaceProxyServerDisabledToolEndToEndTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespaceProxyServerDisabledToolEndToEndTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task NamespaceMcp_DisabledTool_IsHiddenFromListAndNotCallable()
    {
        var ct = TestContext.Current.CancellationToken;
        var (apiKey, slug) = await SeedNamespaceWithDisabledEchoToolAsync();

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

        // Security/correctness contract: ToggleTool(false) must actually take a
        // tool out of service in the namespace — it must NOT appear in
        // ListTools and a CallTool by its name must be rejected (not silently
        // executed). A regression in the IsEnabled filter would re-expose a
        // tool an operator explicitly disabled.
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

    private async Task<(string apiKey, string slug)> SeedNamespaceWithDisabledEchoToolAsync()
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
                Name = $"nsdis-srv-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Stdio,
                Command = "dotnet",
                Arguments = [TestStdioServerLocator.DllPath],
            }
        );
        var sync = await serverManager.SyncTools(server);
        sync.Success.Should().BeTrue($"server-level SyncTools precondition: {sync.Error}");

        var slug = $"e2edis{Guid.NewGuid():N}"[..16];
        var ns = await namespaceManager.Create(
            new McpNamespace { Name = "Disabled NS", Slug = slug }
        );
        var nsServer = await namespaceManager.AddServer(ns, server);

        var nsTool = await nsToolRepo
            .GetByNamespaceServer(nsServer)
            .Include(t => t.McpTool)
            .SingleAsync();
        await namespaceManager.ToggleTool(nsTool, false);

        var apiKey = await apiKeyManager.Create(new ApiKey { Name = $"nsdis-{Guid.NewGuid():N}" });
        return (apiKey.Key, slug);
    }
}
