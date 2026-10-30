using System.Net.Http.Headers;
using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpNamespaceProxyServerCallToolStructuredContentTests
    : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespaceProxyServerCallToolStructuredContentTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task CallToolHandler_UpstreamReturnsStructuredContent_ForwardsItToTheClient()
    {
        var ct = TestContext.Current.CancellationToken;

        var slug = $"nssc{Guid.NewGuid():N}"[..14];
        string apiKey;
        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var serverMgr = sp.GetRequiredService<McpServerManager>();
            var server = await serverMgr.Create(
                new McpServer
                {
                    Name = $"nssc-{Guid.NewGuid():N}",
                    TransportType = McpTransportType.Stdio,
                    Command = "dotnet",
                    Arguments = [TestStdioServerLocator.DllPath],
                }
            );
            (await serverMgr.SyncTools(server)).Success.Should().BeTrue("sync precondition");

            var nsMgr = sp.GetRequiredService<McpNamespaceManager>();
            var ns = await nsMgr.Create(new McpNamespace { Name = "Structured NS", Slug = slug });
            await nsMgr.AddServer(ns, server);

            apiKey = (
                await sp.GetRequiredService<ApiKeyManager>()
                    .Create(new ApiKey { Name = $"nssc-{Guid.NewGuid():N}" })
            ).Key;
        }

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

        // Mirrors McpProxyServerCallToolStructuredContentTests for the namespace
        // proxy: its success branch dropped StructuredContent/IsError the same way.
        var result = await client.CallToolAsync(
            "echo",
            new Dictionary<string, object> { ["message"] = "hello", ["returnStructured"] = true },
            cancellationToken: ct
        );

        result.IsError.Should().Be(false);
        result.StructuredContent.Should().NotBeNull();
        result.StructuredContent!.Value.GetProperty("echoed").GetString().Should().Be("hello");
    }
}
