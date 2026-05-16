using System.Net.Http.Json;
using System.Text.Json;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerCountTokensTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerCountTokensTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task PostCountTokens_WithText_ReturnsPositiveTokenCountJson()
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

        // GET a form to obtain the antiforgery token + cookie. CountTokens is
        // [FromBody] JSON + [ValidateAntiForgeryToken], so the token must travel
        // in the RequestVerificationToken header (the default header name; only
        // FormFieldName is customised in Program.cs).
        var getResp = await client.GetAsync("/McpServers/Create", ct);
        getResp.EnsureSuccessStatusCode();
        var createHtml = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(createHtml), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        var request = new HttpRequestMessage(HttpMethod.Post, "/McpServers/CountTokens")
        {
            Content = JsonContent.Create(
                new { Text = "the quick brown fox jumps over the lazy dog" }
            ),
        };
        request.Headers.Add("RequestVerificationToken", token);

        // CountTokens (and TokenCounterService.CountTokens through it) was
        // entirely uncovered. The token-count UI badge depends on this endpoint
        // returning a positive count for real text; a regression returning 0 or
        // 500 (or breaking the JSON shape) would silently break that affordance.
        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        json.RootElement.GetProperty("count").GetInt32().Should().BeGreaterThan(0);
    }
}
