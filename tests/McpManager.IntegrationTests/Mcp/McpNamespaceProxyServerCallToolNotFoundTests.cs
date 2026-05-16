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

public class McpNamespaceProxyServerCallToolNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespaceProxyServerCallToolNotFoundTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task CallToolHandler_UnknownToolInValidNamespace_SurfacesNotFoundProtocolError()
    {
        var ct = TestContext.Current.CancellationToken;

        string slug = $"nsnf{Guid.NewGuid():N}"[..14];
        string apiKey;
        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            await sp.GetRequiredService<McpNamespaceManager>()
                .Create(new McpNamespace { Name = "NotFound NS", Slug = slug });
            var key = await sp.GetRequiredService<ApiKeyManager>()
                .Create(new ApiKey { Name = $"nsnf-{Guid.NewGuid():N}" });
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

        // The namespace resolves (so the ns==null guard passes) but has no
        // attached servers, so GetEnabledNamespaceTools is empty and any name
        // misses -> CallToolHandler's `nsTool == null` branch throws
        // McpProtocolException "...not found in namespace '<slug>'". The e2e
        // happy-path test only ever calls a real tool, so this branch and the
        // namespace-scoped error message were zero-hit; a regression dropping
        // the guard would NRE on nsTool.McpNamespaceServer -> 500 not a clean
        // protocol error.
        var act = async () =>
            await client.CallToolAsync(
                "definitely-not-a-real-tool",
                new Dictionary<string, object> { ["x"] = "y" },
                cancellationToken: ct
            );

        var thrown = await act.Should().ThrowAsync<McpException>();
        thrown.Which.Message.Should().Contain("not found in namespace");
    }
}
