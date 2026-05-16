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

public class McpNamespaceProxyServerCallToolUnknownNamespaceTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespaceProxyServerCallToolUnknownNamespaceTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task CallToolHandler_UnknownNamespaceSlug_SurfacesNamespaceNotFoundProtocolError()
    {
        var ct = TestContext.Current.CancellationToken;

        string apiKey;
        using (var scope = _factory.Services.CreateScope())
        {
            apiKey = (
                await scope
                    .ServiceProvider.GetRequiredService<ApiKeyManager>()
                    .Create(new ApiKey { Name = $"nscall-{Guid.NewGuid():N}" })
            ).Key;
        }

        var slug = $"missing{Guid.NewGuid():N}"[..14];
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

        // ListTools tolerates an unknown namespace (empty list), but CallTool
        // must reject it: the `ns == null` branch (throw InvalidRequest
        // "Namespace not found") was zero-hit since every call-tool test seeds
        // the namespace. Pins that a stale/typo'd namespace URL yields a clean
        // JSON-RPC error, not an NRE on ns.Slug.
        var act = async () =>
            await client.CallToolAsync(
                "any-tool",
                new Dictionary<string, object>(),
                cancellationToken: ct
            );

        var thrown = await act.Should().ThrowAsync<McpException>();
        thrown.Which.Message.Should().Contain("Namespace not found");
    }
}
