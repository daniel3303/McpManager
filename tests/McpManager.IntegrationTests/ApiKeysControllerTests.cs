using System.Net;
using System.Text.RegularExpressions;
using AngleSharp;
using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public async Task PostDelete_WithExistingKey_RemovesItAndRedirectsToIndex()
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

        ApiKey key;
        using (var scope = _factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<ApiKeyManager>();
            key = await manager.Create(new ApiKey { Name = $"to-delete-{Guid.NewGuid():N}" });
        }

        var getResp = await client.GetAsync("/ApiKeys/Create", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = document
            .QuerySelector("form input[name='AntiForgery']")!
            .GetAttribute("value")!;
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token }
        );

        // Delete (lines 139-152) was uncovered: found-guard bypass ->
        // ApiKeyManager.Delete -> flash + redirect to Index. Asserting the row
        // is gone pins the delete side effect (a regression short-circuiting
        // before Delete would 302 but leave the key — and its access — alive).
        var response = await client.PostAsync($"/ApiKeys/Delete/{key.Id}", form, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<ApiKeyRepository>();
        var reloaded = await repo.Get(key.Id);
        reloaded.Should().BeNull("Delete must remove the API key row");
    }
}
