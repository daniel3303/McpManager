using System.Net.Http.Headers;
using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpProxyServerMultiServerAggregationEndToEndTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpProxyServerMultiServerAggregationEndToEndTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task GlobalMcp_TwoActiveServers_AggregatesBothToolsAndRoutesCallToOwner()
    {
        var ct = TestContext.Current.CancellationToken;
        var (apiKey, secondToolName) = await SeedTwoServersAsync();

        var httpClient = _factory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            apiKey
        );

        await using var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(httpClient.BaseAddress!, "/mcp") },
            httpClient,
            ownsHttpClient: false
        );
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: ct);

        // Core product contract: the unified /mcp endpoint aggregates tools from
        // EVERY active upstream server (not just one), and a CallTool routes to
        // the server that owns the named tool. Every prior e2e uses a single
        // server, so multi-server aggregation + per-call routing was unverified.
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        tools.Should().Contain(t => t.Name == "echo");
        tools.Should().Contain(t => t.Name == secondToolName);

        var result = await client.CallToolAsync(
            "echo",
            new Dictionary<string, object> { ["message"] = "multi-hi" },
            cancellationToken: ct
        );

        result.IsError.Should().NotBe(true);
        result
            .Content.OfType<TextContentBlock>()
            .Should()
            .Contain(c => c.Text != null && c.Text.Contains("multi-hi"));
    }

    private async Task<(string apiKey, string secondToolName)> SeedTwoServersAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var serverManager = sp.GetRequiredService<McpServerManager>();
        var toolRepo = sp.GetRequiredService<McpToolRepository>();
        var apiKeyManager = sp.GetRequiredService<ApiKeyManager>();

        var stdioServer = await serverManager.Create(
            new McpServer
            {
                Name = $"agg-stdio-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Stdio,
                Command = "dotnet",
                Arguments = [TestStdioServerLocator.DllPath],
            }
        );
        var sync = await serverManager.SyncTools(stdioServer);
        sync.Success.Should().BeTrue($"stdio SyncTools precondition: {sync.Error}");

        var httpServer = await serverManager.Create(
            new McpServer
            {
                Name = $"agg-http-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Http,
                Uri = "https://upstream.invalid/mcp",
            }
        );
        var secondToolName = $"weather_{Guid.NewGuid():N}";
        toolRepo.Add(
            new McpTool
            {
                Name = secondToolName,
                Description = "Second server's tool",
                McpServerId = httpServer.Id,
            }
        );
        await toolRepo.SaveChanges();

        var apiKey = await apiKeyManager.Create(new ApiKey { Name = $"agg-{Guid.NewGuid():N}" });
        return (apiKey.Key, secondToolName);
    }
}
