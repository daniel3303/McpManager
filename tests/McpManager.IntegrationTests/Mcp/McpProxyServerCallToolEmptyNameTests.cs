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

public class McpProxyServerCallToolEmptyNameTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpProxyServerCallToolEmptyNameTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task CallToolHandler_EmptyToolName_SurfacesInvalidParamsProtocolError()
    {
        var ct = TestContext.Current.CancellationToken;

        using var scope = _factory.Services.CreateScope();
        var apiKeyManager = scope.ServiceProvider.GetRequiredService<ApiKeyManager>();
        var apiKey = await apiKeyManager.Create(
            new ApiKey { Name = $"emptyname-{Guid.NewGuid():N}" }
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

        // The `string.IsNullOrEmpty(toolName)` guard (throw McpProtocolException
        // InvalidParams "Tool name is required") was zero-hit — every other
        // test passes a concrete name. Pins that a blank name short-circuits
        // before tool resolution as a JSON-RPC error, not an NRE or DB scan.
        var act = async () =>
            await client.CallToolAsync("", new Dictionary<string, object>(), cancellationToken: ct);

        var thrown = await act.Should().ThrowAsync<McpException>();
        thrown.Which.Message.Should().Contain("Tool name is required");
    }
}
