using System.Net.Http.Headers;
using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpProxyServerCallToolNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpProxyServerCallToolNotFoundTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task CallToolHandler_UnknownToolName_SurfacesNotFoundProtocolError()
    {
        var ct = TestContext.Current.CancellationToken;

        using var scope = _factory.Services.CreateScope();
        var apiKeyManager = scope.ServiceProvider.GetRequiredService<ApiKeyManager>();
        var apiKey = await apiKeyManager.Create(
            new ApiKey { Name = $"notfound-{Guid.NewGuid():N}" }
        );

        var httpClient = _factory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            apiKey.Key
        );

        await using var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(httpClient.BaseAddress!, "/mcp") },
            httpClient,
            ownsHttpClient: false
        );
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: ct);

        // CallToolHandler's tool-resolution miss was uncovered: the happy-path
        // e2e test always calls a synced tool, so the `tool == null` branch
        // (throw McpProtocolException InvalidRequest "Tool '...' not found")
        // never ran. With no upstream tools registered, any name misses and the
        // proxy must turn that into a JSON-RPC error the SDK surfaces as
        // McpException — not a 500 and not a silent empty CallToolResult. A
        // regression that dropped the null guard would NRE on tool.McpServer.
        var act = async () =>
            await client.CallToolAsync(
                "definitely-not-a-real-tool",
                new Dictionary<string, object> { ["x"] = "y" },
                cancellationToken: ct
            );

        var thrown = await act.Should().ThrowAsync<McpException>();
        thrown.Which.Message.Should().Contain("not found");
    }
}
