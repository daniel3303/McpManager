using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpNamespacesControllerRemoveServerNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespacesControllerRemoveServerNotFoundTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostRemoveServer_WithUnknownNsServerId_ReturnsNotFound()
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
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token }
        );

        // RemoveServer's `nsServer == null -> NotFound` guard (line 269) was
        // zero-hit: the happy-path test always passes a real link row. An
        // unknown nsServerId must 404, not 500 — a regression dropping the
        // guard would NRE inside NamespaceManager.RemoveServer(null).
        var response = await client.PostAsync(
            $"/McpNamespaces/RemoveServer/{Guid.NewGuid()}?nsServerId={Guid.NewGuid()}",
            form,
            ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
