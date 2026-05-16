using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class LiveLogsControllerIndexTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public LiveLogsControllerIndexTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetIndex_WithSeededServer_RendersServerInFilterList()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            }
        );
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        var name = $"livelogs-{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            await scope
                .ServiceProvider.GetRequiredService<McpServerManager>()
                .Create(
                    new McpServer
                    {
                        Name = name,
                        TransportType = McpTransportType.Http,
                        Uri = "https://upstream.invalid/mcp",
                    }
                );
        }

        // Only Poll was covered; the whole Index action was zero-hit. Index
        // runs the ServerListItem projection (ordered) into ViewData["Servers"]
        // and renders the live-log viewer's server filter. A regression in that
        // query/projection or the view would 500 the page operators use to
        // watch logs — asserting the seeded server appears pins it end to end.
        var response = await client.GetAsync("/LiveLogs", ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain(name);
    }
}
