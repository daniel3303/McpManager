using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpPlaygroundControllerGetToolsQueryStringNotFoundTests
    : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpPlaygroundControllerGetToolsQueryStringNotFoundTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task GetGetTools_UnknownServerIdViaQueryString_HitsActionNotFoundGuard()
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

        // GetTools has no route template, so it binds serverId from the query
        // string; the existing sibling test uses a path-segment URL which 404s
        // at *routing* without entering the action, leaving the `server ==
        // null -> NotFound()` guard zero-hit. This query-string URL runs the
        // action so the guard executes — a regression dropping it would NRE on
        // the tool query for any stale server id.
        var response = await client.GetAsync(
            $"/McpPlayground/GetTools?serverId={Guid.NewGuid()}",
            ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
