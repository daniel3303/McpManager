using System.Net.Http.Headers;
using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpNamespaceProxyServerCallToolEmptyNameTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespaceProxyServerCallToolEmptyNameTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task CallToolHandler_EmptyToolName_SurfacesInvalidParamsProtocolError()
    {
        var ct = TestContext.Current.CancellationToken;

        string slug = $"nsen{Guid.NewGuid():N}"[..14];
        string apiKey;
        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            await sp.GetRequiredService<McpNamespaceManager>()
                .Create(new McpNamespace { Name = "EmptyName NS", Slug = slug });
            var key = await sp.GetRequiredService<ApiKeyManager>()
                .Create(new ApiKey { Name = $"nsen-{Guid.NewGuid():N}" });
            apiKey = key.Key;
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

        // The `string.IsNullOrEmpty(toolName)` guard (throw InvalidParams
        // "Tool name is required") fires before namespace resolution and was
        // zero-hit — every other test passes a concrete name. Pins that a
        // blank name short-circuits as a JSON-RPC error, not an NRE.
        var act = async () =>
            await client.CallToolAsync("", new Dictionary<string, object>(), cancellationToken: ct);

        var thrown = await act.Should().ThrowAsync<McpException>();
        thrown.Which.Message.Should().Contain("Tool name is required");
    }
}
