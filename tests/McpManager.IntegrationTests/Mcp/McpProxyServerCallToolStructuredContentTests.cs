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

public class McpProxyServerCallToolStructuredContentTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpProxyServerCallToolStructuredContentTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task CallToolHandler_UpstreamReturnsStructuredContent_ForwardsItToTheClient()
    {
        var ct = TestContext.Current.CancellationToken;

        string apiKey;
        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var mgr = sp.GetRequiredService<McpServerManager>();
            var server = await mgr.Create(
                new McpServer
                {
                    Name = $"sc-{Guid.NewGuid():N}",
                    TransportType = McpTransportType.Stdio,
                    Command = "dotnet",
                    Arguments = [TestStdioServerLocator.DllPath],
                }
            );
            (await mgr.SyncTools(server)).Success.Should().BeTrue("sync precondition");

            apiKey = (
                await sp.GetRequiredService<ApiKeyManager>()
                    .Create(new ApiKey { Name = $"sc-{Guid.NewGuid():N}" })
            ).Key;
        }

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

        // The upstream tool returns a CallToolResult with StructuredContent and
        // IsError = false explicitly set. CallToolHandler's success branch used to
        // build its response from result.Content alone, silently dropping both
        // fields. A regression here would make structuredContent (and isError on
        // success) disappear for every client going through this proxy.
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
