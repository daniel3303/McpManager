using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerSyncNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerSyncNotFoundTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task PostSync_WithUnknownServerId_RedirectsToIndexWithError()
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

        // Sync's server == null guard (flash error + redirect to Index) was
        // zero-hit: every existing Sync test seeds the server first. A stale
        // "Sync" button on a since-deleted server must redirect gracefully,
        // not 500 (SyncTools would NRE on a null server otherwise).
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token }
        );
        var response = await client.PostAsync($"/McpServers/Sync/{Guid.NewGuid()}", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response
            .Headers.Location!.ToString()
            .Should()
            .Contain("/mcpservers", "the not-found Sync must redirect back to the Index action");
    }
}
