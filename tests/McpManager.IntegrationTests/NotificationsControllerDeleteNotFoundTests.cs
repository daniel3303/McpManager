using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class NotificationsControllerDeleteNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public NotificationsControllerDeleteNotFoundTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostDelete_WithUnknownId_RedirectsToIndexWithError()
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

        var getResp = await client.GetAsync("/McpServers/Create", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        // Delete's notification == null guard (flash error + redirect to Index)
        // was zero-hit: every Delete test seeds a notification for the user.
        // Deleting one already removed (or belonging to another user, filtered
        // out by GetByUser) must redirect, not 500 on the null handed to
        // NotificationManager.Delete.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token }
        );
        var response = await client.PostAsync($"/Notifications/Delete/{Guid.NewGuid()}", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response
            .Headers.Location!.ToString()
            .Should()
            .Contain("/notifications", "an unknown id must redirect back to the Index action");
    }
}
