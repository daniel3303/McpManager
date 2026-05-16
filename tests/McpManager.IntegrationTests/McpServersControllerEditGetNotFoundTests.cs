using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerEditGetNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerEditGetNotFoundTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task GetEdit_WithUnknownId_RedirectsToIndexWithError()
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

        // The Edit GET not-found branch (server == null -> flash error +
        // redirect to Index) was uncovered: the existing Edit tests always
        // seed the server. A stale bookmarked /McpServers/Edit/{id} link must
        // degrade to a redirect, never a 404/500 — a regression dropping the
        // null guard would NRE in MapServerToDto(null).
        var response = await client.GetAsync($"/McpServers/Edit/{Guid.NewGuid()}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().EndWithEquivalentOf("/mcpservers");
    }
}
