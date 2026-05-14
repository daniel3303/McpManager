using System.Net;
using System.Text.RegularExpressions;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpNamespacesControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespacesControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task PostCreate_WithValidNameAndSlug_RedirectsToShowOfNewNamespace()
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

        var getResponse = await client.GetAsync("/McpNamespaces/Create", ct);
        getResponse.EnsureSuccessStatusCode();
        var html = await getResponse.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var antiForgeryToken = document
            .QuerySelector("form input[name='AntiForgery']")!
            .GetAttribute("value")!;

        // A unique slug per run keeps the test idempotent against the shared
        // fixture DB; the slug constraint is ^[a-z0-9][a-z0-9-]*$.
        var slug = "ns-" + Guid.NewGuid().ToString("n")[..8];
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = antiForgeryToken,
                ["Name"] = "Integration test namespace",
                ["Slug"] = slug,
                ["RateLimitRequestsPerMinute"] = "60",
            }
        );

        var response = await client.PostAsync("/McpNamespaces/Create", form, ct);

        // Create() ends in RedirectToAction(Show, new { id = ns.Id }). A
        // regression in McpNamespaces policy, antiforgery, the
        // McpNamespaceManager.Create slug-uniqueness path, or the redirect
        // target would surface here as 200/400/403 or a wrong Location.
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        Regex
            .IsMatch(
                response.Headers.Location!.ToString(),
                @"^/mcpnamespaces/show/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
                RegexOptions.IgnoreCase
            )
            .Should()
            .BeTrue(
                $"Location should match /mcpnamespaces/show/<guid>, was '{response.Headers.Location}'"
            );
    }
}
