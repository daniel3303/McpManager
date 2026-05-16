using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpPlaygroundControllerGetToolFormNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpPlaygroundControllerGetToolFormNotFoundTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task GetToolForm_WithUnknownToolId_Returns404()
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

        // GetToolForm's tool == null guard was zero-hit: every GetToolForm
        // test seeds a tool. A stale playground panel requesting a tool that
        // was removed must 404 (so the UI clears it), not 500 on the null
        // tool passed to ParseToolSchema.
        var response = await client.GetAsync(
            $"/McpPlayground/GetToolForm?toolId={Guid.NewGuid()}",
            ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
