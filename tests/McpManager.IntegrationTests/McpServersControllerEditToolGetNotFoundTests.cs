using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerEditToolGetNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerEditToolGetNotFoundTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task GetEditTool_WithUnknownToolId_ReturnsNotFound()
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

        // The EditTool GET `tool == null -> NotFound` guard (line 424) was
        // zero-hit: the existing EditTool test always seeds the tool. An
        // unknown toolId must 404, not 500 — a regression dropping the guard
        // would NRE on tool.InputSchema during the schema parse.
        var response = await client.GetAsync(
            $"/McpServers/EditTool/{Guid.NewGuid()}/{Guid.NewGuid()}",
            ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
