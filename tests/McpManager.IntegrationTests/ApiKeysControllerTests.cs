using System.Net;
using System.Text.RegularExpressions;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class ApiKeysControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ApiKeysControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task PostCreate_WithValidNameAndToken_RedirectsToShowOfNewKey()
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

        var getResponse = await client.GetAsync("/ApiKeys/Create", ct);
        getResponse.EnsureSuccessStatusCode();
        var html = await getResponse.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var antiForgeryToken = document
            .QuerySelector("form input[name='AntiForgery']")!
            .GetAttribute("value")!;

        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = antiForgeryToken,
                ["Name"] = "Integration test key",
            }
        );

        var response = await client.PostAsync("/ApiKeys/Create", form, ct);

        // Successful Create() ends in RedirectToAction(Show, new { id = apiKey.Id }).
        // A regression in the [Authorize(Policy = "ApiKeys")] gate, the
        // antiforgery wiring, ApiKeyManager.Create, or the redirect target
        // would surface here as a 200 (form re-render), 400, 403, or a mismatched
        // Location.
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        Regex
            .IsMatch(
                response.Headers.Location!.ToString(),
                @"^/apikeys/show/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
                RegexOptions.IgnoreCase
            )
            .Should()
            .BeTrue(
                $"Location should match /apikeys/show/<guid>, was '{response.Headers.Location}'"
            );
    }
}
