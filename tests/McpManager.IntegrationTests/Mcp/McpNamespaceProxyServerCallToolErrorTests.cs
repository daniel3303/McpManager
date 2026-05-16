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

public class McpNamespaceProxyServerCallToolErrorTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespaceProxyServerCallToolErrorTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task CallToolHandler_UpstreamCallFails_ReturnsIsErrorResultNotException()
    {
        var ct = TestContext.Current.CancellationToken;

        var slug = $"nserr{Guid.NewGuid():N}"[..14];
        string apiKey;
        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var serverMgr = sp.GetRequiredService<McpServerManager>();
            var server = await serverMgr.Create(
                new McpServer
                {
                    Name = $"nserr-{Guid.NewGuid():N}",
                    TransportType = McpTransportType.Stdio,
                    Command = "dotnet",
                    Arguments = [TestStdioServerLocator.DllPath],
                }
            );
            (await serverMgr.SyncTools(server)).Success.Should().BeTrue("sync precondition");

            var nsMgr = sp.GetRequiredService<McpNamespaceManager>();
            var ns = await nsMgr.Create(new McpNamespace { Name = "Err NS", Slug = slug });
            // AddServer copies the synced tools into the namespace while the
            // command still works; break it afterwards so the tool resolves but
            // the upstream spawn fails at call time.
            await nsMgr.AddServer(ns, server);
            server.Command = "this-executable-does-not-exist-xyz";
            server.Arguments = [];
            await serverMgr.Update(server);

            apiKey = (
                await sp.GetRequiredService<ApiKeyManager>()
                    .Create(new ApiKey { Name = $"nserr-{Guid.NewGuid():N}" })
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

        // The namespace happy-path e2e only calls a working tool. When the
        // upstream call fails, CallToolHandler must map result.Success==false to
        // a CallToolResult { IsError = true } (a readable tool error) — not a
        // thrown protocol exception, not a swallowed success.
        var result = await client.CallToolAsync(
            "echo",
            new Dictionary<string, object> { ["message"] = "boom" },
            cancellationToken: ct
        );

        result.IsError.Should().Be(true);
        result.Content.OfType<TextContentBlock>().Should().NotBeEmpty();
    }
}
