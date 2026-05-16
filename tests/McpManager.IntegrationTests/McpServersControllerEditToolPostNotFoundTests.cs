using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerEditToolPostNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerEditToolPostNotFoundTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostEditTool_WithUnknownToolId_Returns404()
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

        // The EditTool POST tool == null guard returns NotFound() before
        // UpdateToolCustomization runs. It was zero-hit (the GET not-found
        // test covers line 424; every EditTool POST test seeds the tool).
        // Submitting an edit for a tool removed by a re-sync must 404, not
        // 500 on the null tool handed to the manager.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token }
        );
        var response = await client.PostAsync(
            $"/McpServers/EditTool/{Guid.NewGuid()}/{Guid.NewGuid()}",
            form,
            ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
