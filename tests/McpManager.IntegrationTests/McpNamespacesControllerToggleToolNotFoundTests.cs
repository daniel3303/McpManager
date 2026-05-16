using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpNamespacesControllerToggleToolNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespacesControllerToggleToolNotFoundTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostToggleTool_UnknownNsToolId_ReturnsNotFound()
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

        // The found path is pinned elsewhere; the `nsTool == null` guard
        // (return NotFound) was zero-hit. Pins that toggling an unknown
        // namespace-tool 404s instead of NRE'ing in NamespaceManager.ToggleTool.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["nsToolId"] = Guid.NewGuid().ToString(),
                ["isEnabled"] = "false",
            }
        );

        var response = await client.PostAsync("/McpNamespaces/ToggleTool", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
