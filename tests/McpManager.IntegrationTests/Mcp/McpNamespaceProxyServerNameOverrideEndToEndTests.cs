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
using ModelContextProtocol.Protocol;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpNamespaceProxyServerNameOverrideEndToEndTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespaceProxyServerNameOverrideEndToEndTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task NamespaceMcp_ToolWithNameOverride_AdvertisesOverrideAndRoutesCallToUpstream()
    {
        var ct = TestContext.Current.CancellationToken;
        var (apiKey, slug) = await SeedNamespaceWithRenamedEchoToolAsync();

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

        // Contract: a NameOverride renames the tool's public identity in the
        // namespace. ListTools must advertise the override name and hide the
        // original; CallTool by the override name must still route to and
        // execute the original upstream Echo tool. A routing bug here either
        // hides renamed tools or sends the call to the wrong/no tool.
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        tools.Should().Contain(t => t.Name == "echo_renamed");
        tools.Should().NotContain(t => t.Name == "echo");

        var result = await client.CallToolAsync(
            "echo_renamed",
            new Dictionary<string, object> { ["message"] = "ns-override-hi" },
            cancellationToken: ct
        );

        result.IsError.Should().NotBe(true);
        result
            .Content.OfType<TextContentBlock>()
            .Should()
            .Contain(c => c.Text != null && c.Text.Contains("ns-override-hi"));
    }

    private async Task<(string apiKey, string slug)> SeedNamespaceWithRenamedEchoToolAsync()
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
                Name = $"nsovr-srv-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Stdio,
                Command = "dotnet",
                Arguments = [TestStdioServerLocator.DllPath],
            }
        );
        var sync = await serverManager.SyncTools(server);
        sync.Success.Should().BeTrue($"server-level SyncTools precondition: {sync.Error}");

        var slug = $"e2eovr{Guid.NewGuid():N}"[..16];
        var ns = await namespaceManager.Create(
            new McpNamespace { Name = "Override NS", Slug = slug }
        );
        var nsServer = await namespaceManager.AddServer(ns, server);

        var nsTool = await nsToolRepo
            .GetByNamespaceServer(nsServer)
            .Include(t => t.McpTool)
            .SingleAsync();
        await namespaceManager.UpdateToolOverride(nsTool, "echo_renamed", null);

        var apiKey = await apiKeyManager.Create(new ApiKey { Name = $"nsovr-{Guid.NewGuid():N}" });
        return (apiKey.Key, slug);
    }
}
