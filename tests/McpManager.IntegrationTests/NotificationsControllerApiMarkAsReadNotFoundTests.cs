using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class NotificationsControllerApiMarkAsReadNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public NotificationsControllerApiMarkAsReadNotFoundTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostApiMarkAsRead_UnknownId_ReturnsNotFound()
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

        var indexResp = await client.GetAsync("/Notifications", ct);
        indexResp.EnsureSuccessStatusCode();
        var html = await indexResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("form input[name='AntiForgery']")!.GetAttribute("value")!;

        // ApiMarkAsRead binds `id` from the query string; its `notification ==
        // null -> NotFound()` guard was zero-hit (the bell UI only marks real
        // notifications). A regression dropping it would NRE in MarkAsRead for
        // any stale/foreign notification id the client posts.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token }
        );

        var response = await client.PostAsync(
            $"/Notifications/ApiMarkAsRead?id={Guid.NewGuid()}",
            form,
            ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
