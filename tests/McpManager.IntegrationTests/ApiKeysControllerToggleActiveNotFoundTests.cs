using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class ApiKeysControllerToggleActiveNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ApiKeysControllerToggleActiveNotFoundTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostToggleActive_WithUnknownId_RedirectsToIndexWithError()
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

        var getResp = await client.GetAsync("/ApiKeys/Create", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        // ToggleActive's apiKey == null guard (flash error + redirect to Index)
        // was zero-hit: every ToggleActive test seeds the key. Toggling a key
        // removed in another tab must redirect, not 500 on the null key handed
        // to ApiKeyManager.ToggleActive.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token }
        );
        var response = await client.PostAsync($"/ApiKeys/ToggleActive/{Guid.NewGuid()}", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response
            .Headers.Location!.ToString()
            .Should()
            .Contain("/apikeys", "an unknown id must redirect back to the Index action");
    }
}
