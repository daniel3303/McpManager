using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class NotificationsControllerShowNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public NotificationsControllerShowNotFoundTests(WebFactoryFixture factory) =>
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

        // Show's notification == null guard (flash error + redirect to Index)
        // was zero-hit: every Show test seeds a notification for the signed-in
        // user. Following a stale notification link (or one belonging to
        // another user, filtered out by GetByUser) must redirect gracefully,
        // not 500 on the MarkAsRead/Url dereference below.
        var response = await client.GetAsync($"/Notifications/Show/{Guid.NewGuid()}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response
            .Headers.Location!.ToString()
            .Should()
            .Contain(
                "/notifications",
                "an unknown notification id must redirect to the Index action"
            );
    }
}
