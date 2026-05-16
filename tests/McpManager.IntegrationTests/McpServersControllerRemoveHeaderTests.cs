using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerRemoveHeaderTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerRemoveHeaderTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task PostRemoveHeader_RemovesRowAtIndexAndReturnsRemaining()
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
        var formDoc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(createHtml), ct);
        var token = formDoc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        var keepValue = $"keep-{Guid.NewGuid():N}";
        var dropValue = $"drop-{Guid.NewGuid():N}";
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Name"] = "any",
                ["CustomHeaders[0].Key"] = "X-Drop",
                ["CustomHeaders[0].Value"] = dropValue,
                ["CustomHeaders[1].Key"] = "X-Keep",
                ["CustomHeaders[1].Value"] = keepValue,
                ["index"] = "0",
            }
        );

        // RemoveHeader's bounds-checked RemoveAt + _CustomHeadersForm partial
        // was uncovered (only AddHeader had a test). Removing index 0 must drop
        // exactly that row and re-render the survivor; a regression in the
        // bounds check or RemoveAt would 500 or delete the wrong header.
        var response = await client.PostAsync("/McpServers/RemoveHeader", form, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        html.Should().Contain(keepValue);
        html.Should().NotContain(dropValue, "the header at the removed index must be gone");
    }
}
