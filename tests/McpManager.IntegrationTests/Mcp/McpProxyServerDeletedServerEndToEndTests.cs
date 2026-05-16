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

public class McpProxyServerDeletedServerEndToEndTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpProxyServerDeletedServerEndToEndTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task GlobalMcp_AfterServerDeleted_ItsToolsAreNoLongerListedOrCallable()
    {
        var ct = TestContext.Current.CancellationToken;
        var apiKey = await SeedSyncedThenDeletedServerAsync();

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

        // Data-integrity + protocol contract: deleting a server must take its
        // tools out of service on the unified endpoint — not listed, not
        // callable. A missing delete-cascade would leave orphan tool rows (the
        // echo tool re-served or a NRE on the null McpServer nav in the join).
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        tools.Should().NotContain(t => t.Name == "echo");

        var act = async () =>
            await client.CallToolAsync(
                "echo",
                new Dictionary<string, object> { ["message"] = "ghost" },
                cancellationToken: ct
            );

        var thrown = await act.Should().ThrowAsync<McpException>();
        thrown.Which.Message.Should().Contain("not found");
    }

    private async Task<string> SeedSyncedThenDeletedServerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var serverManager = sp.GetRequiredService<McpServerManager>();
        var apiKeyManager = sp.GetRequiredService<ApiKeyManager>();

        var server = await serverManager.Create(
            new McpServer
            {
                Name = $"del-srv-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Stdio,
                Command = "dotnet",
                Arguments = [TestStdioServerLocator.DllPath],
            }
        );
        var sync = await serverManager.SyncTools(server);
        sync.Success.Should()
            .BeTrue($"SyncTools precondition (echo must exist first): {sync.Error}");

        // Now delete the server it belongs to — the echo tool must go with it.
        await serverManager.Delete(server);

        var apiKey = await apiKeyManager.Create(new ApiKey { Name = $"del-{Guid.NewGuid():N}" });
        return apiKey.Key;
    }
}
