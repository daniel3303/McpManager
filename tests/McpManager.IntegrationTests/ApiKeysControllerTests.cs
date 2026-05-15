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

    [Fact]
    public async Task PostToggleActive_WithActiveKey_DeactivatesItAndRedirectsToShow()
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
            key = await manager.Create(new ApiKey { Name = $"toggle-{Guid.NewGuid():N}" });
        }
        key.IsActive.Should().BeTrue("new keys default to active");

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

        // ToggleActive (lines 166-179) was uncovered: found-guard bypass ->
        // ApiKeyManager.ToggleActive -> flash + redirect to Show. Asserting the
        // flipped IsActive pins the state mutation (a regression no-op'ing the
        // toggle would 302 but leave a revoked key still authenticating).
        var response = await client.PostAsync($"/ApiKeys/ToggleActive/{key.Id}", form, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<ApiKeyRepository>();
        var reloaded = await repo.Get(key.Id);
        reloaded!.IsActive.Should().BeFalse("ToggleActive must flip an active key to inactive");
    }

    [Fact]
    public async Task PostEdit_WithValidName_RenamesKeyAndReturnsSuccessJson()
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
            key = await manager.Create(new ApiKey { Name = $"before-{Guid.NewGuid():N}" });
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
        var newName = $"after-{Guid.NewGuid():N}";
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token, ["Name"] = newName }
        );

        // Edit POST (lines 122-134) was uncovered: found-guard + ModelState
        // valid -> ApiKeyManager.Rename -> Json { success, redirect }. Asserting
        // the persisted rename pins that Rename ran (a regression returning the
        // success JSON without renaming would silently no-op the edit UI).
        var response = await client.PostAsync($"/ApiKeys/Edit/{key.Id}", form, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("\"success\":true");

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<ApiKeyRepository>();
        var reloaded = await repo.Get(key.Id);
        reloaded!.Name.Should().Be(newName, "Edit POST must persist the rename");
    }

    [Fact]
    public async Task GetIndex_WithSearchFilter_ReturnsOnlyMatchingKey()
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

        var token = Guid.NewGuid().ToString("n")[..8];
        var otherName = $"other-{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<ApiKeyManager>();
            await manager.Create(new ApiKey { Name = $"match-{token}" });
            await manager.Create(new ApiKey { Name = otherName });
        }

        // Index (lines 36-54) was uncovered — no test hit the API-keys list
        // page. The Name Contains filter is the branch worth pinning: a
        // regression dropping the Where would also list the non-matching key
        // (leaking unrelated key names into a filtered admin view).
        var response = await client.GetAsync($"/ApiKeys?Search={token}", ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain($"match-{token}");
        body.Should().NotContain(otherName, "the search filter must exclude non-matches");
    }

    [Fact]
    public async Task GetShow_WithUnknownId_RedirectsToIndexWithError()
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

        // Show's not-found branch (lines 64-68) was uncovered. A stale
        // bookmarked /ApiKeys/Show/{id} link must degrade to a flashed error +
        // redirect to Index, never a 404/500 — a regression dropping the null
        // guard would surface a raw error to an admin clicking an old link.
        var response = await client.GetAsync($"/ApiKeys/Show/{Guid.NewGuid()}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response
            .Headers.Location!.ToString()
            .Should()
            .BeEquivalentTo("/apikeys", "Index redirect target (URLs are lowercased)");
    }
}
