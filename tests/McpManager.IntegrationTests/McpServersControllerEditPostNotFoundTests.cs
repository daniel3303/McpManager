using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerEditPostNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerEditPostNotFoundTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostEdit_UnknownId_FlashesErrorAndRedirectsToIndex()
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
        var createHtml = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(createHtml), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        // The existing not-found test covers GET Edit; the POST Edit
        // `server == null` guard (flash error + RedirectToAction(Index)) was
        // zero-hit because every POST-Edit test seeds the server first. Pins
        // that posting to an unknown id can't fall through to sync/persist.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Name"] = "ghost",
                ["TransportType"] = "Http",
                ["Uri"] = "https://upstream.invalid/mcp",
            }
        );

        var resp = await client.PostAsync($"/McpServers/Edit/{Guid.NewGuid()}", form, ct);

        resp.StatusCode.Should().Be(HttpStatusCode.Found);
        resp.Headers.Location!.ToString().Should().Contain("/mcpservers");
    }
}
