using System.Net.Http.Headers;
using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpProxyServerCallToolErrorTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpProxyServerCallToolErrorTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task CallToolHandler_UpstreamCallFails_ReturnsIsErrorResultNotException()
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
                    Name = $"err-{Guid.NewGuid():N}",
                    TransportType = McpTransportType.Stdio,
                    Command = "dotnet",
                    Arguments = [TestStdioServerLocator.DllPath],
                }
            );
            (await mgr.SyncTools(server)).Success.Should().BeTrue("sync precondition");
            // Break the command AFTER the echo tool row is synced: the tool
            // still resolves but spawning the upstream now fails.
            server.Command = "this-executable-does-not-exist-xyz";
            server.Arguments = [];
            await mgr.Update(server);

            apiKey = (
                await sp.GetRequiredService<ApiKeyManager>()
                    .Create(new ApiKey { Name = $"err-{Guid.NewGuid():N}" })
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

        // The happy-path e2e test only ever calls a working tool. When the
        // upstream call fails, CallToolHandler must map result.Success==false to
        // a CallToolResult { IsError = true } (a normal tool error the client
        // can read) — NOT a thrown protocol exception. A regression here would
        // either 500 or swallow the failure as a success.
        var result = await client.CallToolAsync(
            "echo",
            new Dictionary<string, object> { ["message"] = "boom" },
            cancellationToken: ct
        );

        result.IsError.Should().Be(true);
        result.Content.OfType<TextContentBlock>().Should().NotBeEmpty();
    }
}
