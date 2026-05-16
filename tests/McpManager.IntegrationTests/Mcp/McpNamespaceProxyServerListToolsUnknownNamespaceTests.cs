using System.Net.Http.Headers;
using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpNamespaceProxyServerListToolsUnknownNamespaceTests
    : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespaceProxyServerListToolsUnknownNamespaceTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task ListToolsHandler_UnknownNamespaceSlug_ReturnsEmptyToolListNotError()
    {
        var ct = TestContext.Current.CancellationToken;

        string apiKey;
        using (var scope = _factory.Services.CreateScope())
        {
            apiKey = (
                await scope
                    .ServiceProvider.GetRequiredService<ApiKeyManager>()
                    .Create(new ApiKey { Name = $"nslist-{Guid.NewGuid():N}" })
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

        // The route matches any slug, but GetBySlug returns null for an
        // unknown one -> ListToolsHandler's `ns == null` branch must yield an
        // empty tool list, NOT throw. The happy-path test always seeds the
        // namespace; a regression dropping the null guard would 500 the
        // discovery call for any stale/typo'd namespace URL.
        var tools = await client.ListToolsAsync(cancellationToken: ct);

        tools.Should().BeEmpty();
    }
}
