using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class UsersControllerEditNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public UsersControllerEditNotFoundTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetEdit_WithUnknownId_RedirectsToIndex()
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

        // The Edit GET not-found branch (user == null -> flash error + redirect
        // to Index) was uncovered: existing Edit tests always pass a seeded id. A
        // stale bookmarked /Users/Edit/{id} link must degrade to a redirect,
        // never a 404/500 — a regression dropping the null guard would NRE on
        // user.Claims instead of this clean redirect.
        var response = await client.GetAsync($"/Users/Edit?id={Guid.NewGuid()}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().EndWithEquivalentOf("/users");
    }
}
