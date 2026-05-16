using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpClientFactoryHttpTransportTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpClientFactoryHttpTransportTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task CheckHealth_UnreachableHttpServer_RoutesThroughHttpTransportAndFails()
    {
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpServerManager>();

        // Only the Stdio CheckHealth path is covered; the McpClientFactory
        // default switch arm (`_ => CreateHttpClient`) for Http/SSE transport
        // was zero-hit. An unreachable .invalid host makes McpClient.CreateAsync
        // fail deterministically; CheckHealth must catch, stamp LastError, and
        // return false — a regression in the Http transport arm would 500 the
        // operator health check instead.
        var server = await sut.Create(
            new McpServer
            {
                Name = $"unreachable-http-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Http,
                Uri = "https://upstream.invalid/mcp",
            }
        );

        var healthy = await sut.CheckHealth(server);

        healthy.Should().BeFalse();
        server.LastError.Should().Contain("Failed to connect");
    }
}
