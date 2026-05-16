using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpNamespacesControllerDeleteNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespacesControllerDeleteNotFoundTests(WebFactoryFixture factory) =>
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

        var getResp = await client.GetAsync("/McpNamespaces/Create", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("form input[name='AntiForgery']")!.GetAttribute("value")!;

        // The Delete POST ns == null guard (flash error + redirect to Index)
        // was zero-hit: every Delete test seeds the namespace first. Clicking
        // a stale "Delete" button on a namespace removed in another tab must
        // redirect gracefully, not 500 on the null namespace passed to
        // NamespaceManager.Delete.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token }
        );
        var response = await client.PostAsync($"/McpNamespaces/Delete/{Guid.NewGuid()}", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response
            .Headers.Location!.ToString()
            .Should()
            .Contain("/mcpnamespaces", "an unknown id must redirect back to the Index action");
    }
}
