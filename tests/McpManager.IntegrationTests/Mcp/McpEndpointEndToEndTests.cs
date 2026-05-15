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

public class McpEndpointEndToEndTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpEndpointEndToEndTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task PostMcp_WithValidApiKey_AggregatesUpstreamEchoToolAndCallsIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var (apiKey, _) = await SeedStdioUpstreamAndApiKeyAsync();

        // WebApplicationFactory.CreateClient backs the HttpClient with the
        // in-memory TestServer; HttpClientTransport drives the MCP JSON-RPC
        // protocol over that client just like a real network round-trip
        // (httpOptions.Stateless = true in Program.cs means no session glue
        // is needed). Adding the Bearer header preemptively because the
        // SDK does not let us inject auth into the transport's own client.
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

        // 1. ListToolsAsync hits McpProxyServer.ListToolsHandler over the wire
        //    and must return the synced echo tool from TestStdioServer.
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        tools.Should().Contain(t => t.Name == "echo");

        // 2. CallToolAsync round-trips through McpProxyServer.CallToolHandler
        //    -> McpServerManager.CallTool -> McpClientFactory stdio path ->
        //    TestStdioServer's EchoTools.Echo. The response content carries
        //    the upstream's "echo: <message>" string.
        var result = await client.CallToolAsync(
            "echo",
            new Dictionary<string, object> { ["message"] = "hello" },
            cancellationToken: ct
        );

        result.IsError.Should().NotBe(true);
        result
            .Content.OfType<TextContentBlock>()
            .Should()
            .Contain(c => c.Text != null && c.Text.Contains("hello"));
    }

    private async Task<(string apiKey, Guid serverId)> SeedStdioUpstreamAndApiKeyAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var serverManager = scope.ServiceProvider.GetRequiredService<McpServerManager>();
        var apiKeyManager = scope.ServiceProvider.GetRequiredService<ApiKeyManager>();

        var server = await serverManager.Create(
            new McpServer
            {
                Name = $"e2e-stdio-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Stdio,
                Command = "dotnet",
                Arguments = [TestStdioServerLocator.DllPath],
            }
        );

        var sync = await serverManager.SyncTools(server);
        sync.Success.Should().BeTrue($"SyncTools is the e2e precondition: {sync.Error}");

        var apiKey = await apiKeyManager.Create(new ApiKey { Name = $"e2e-{Guid.NewGuid():N}" });
        return (apiKey.Key, server.Id);
    }
}
