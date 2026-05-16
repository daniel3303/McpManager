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

public class McpNamespaceProxyServerMultiServerEndToEndTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespaceProxyServerMultiServerEndToEndTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task NamespaceMcp_TwoAttachedServers_AggregatesBothToolsAndRoutesCall()
    {
        var ct = TestContext.Current.CancellationToken;
        var (apiKey, slug, secondToolName) = await SeedNamespaceWithTwoServersAsync();

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

        // Core contract for the namespace endpoint: a namespace with multiple
        // attached servers aggregates EVERY enabled tool across them and routes
        // a call to the owning server. This is the distinct
        // GetEnabledNamespaceTools join path; prior namespace e2e only ever
        // attached one server.
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        tools.Should().Contain(t => t.Name == "echo");
        tools.Should().Contain(t => t.Name == secondToolName);

        var result = await client.CallToolAsync(
            "echo",
            new Dictionary<string, object> { ["message"] = "ns-multi-hi" },
            cancellationToken: ct
        );

        result.IsError.Should().NotBe(true);
        result
            .Content.OfType<TextContentBlock>()
            .Should()
            .Contain(c => c.Text != null && c.Text.Contains("ns-multi-hi"));
    }

    private async Task<(
        string apiKey,
        string slug,
        string secondToolName
    )> SeedNamespaceWithTwoServersAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var serverManager = sp.GetRequiredService<McpServerManager>();
        var namespaceManager = sp.GetRequiredService<McpNamespaceManager>();
        var toolRepo = sp.GetRequiredService<McpToolRepository>();
        var apiKeyManager = sp.GetRequiredService<ApiKeyManager>();

        var stdioServer = await serverManager.Create(
            new McpServer
            {
                Name = $"nsmulti-stdio-{Guid.NewGuid():N}",
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
                Name = $"nsmulti-http-{Guid.NewGuid():N}",
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

        var slug = $"e2ensmulti{Guid.NewGuid():N}"[..16];
        var ns = await namespaceManager.Create(
            new McpNamespace { Name = "Multi Server NS", Slug = slug }
        );
        await namespaceManager.AddServer(ns, stdioServer);
        await namespaceManager.AddServer(ns, httpServer);

        var apiKey = await apiKeyManager.Create(
            new ApiKey { Name = $"nsmulti-{Guid.NewGuid():N}" }
        );
        return (apiKey.Key, slug, secondToolName);
    }
}
