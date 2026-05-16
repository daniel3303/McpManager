using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpNamespacesControllerShowNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespacesControllerShowNotFoundTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task GetShow_WithUnknownId_RedirectsToIndexWithError()
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

        // Show's not-found branch (ns == null -> flash error + redirect to
        // Index) was uncovered: the existing Show test always seeds the
        // namespace. A stale bookmarked /McpNamespaces/Show/{id} link must
        // degrade to a redirect, never a 404/500 — a regression dropping the
        // null guard would NRE on the view model instead of this clean redirect.
        var response = await client.GetAsync($"/McpNamespaces/Show/{Guid.NewGuid()}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().EndWithEquivalentOf("/mcpnamespaces");
    }
}
