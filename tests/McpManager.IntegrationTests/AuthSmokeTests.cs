using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class AuthSmokeTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public AuthSmokeTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetLogin_RendersFormWithResolvedAction()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/Auth/Login", ct);

        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);

        // Asserting on the resolved action attribute catches typos in
        // asp-action / asp-controller — a broken tag helper renders action=""
        // instead of throwing.
        var form = document.QuerySelector("form#loginForm");
        form.Should().NotBeNull("the login view must render the login form");
        form.GetAttribute("action").Should().BeEquivalentTo("/Auth/Login");
    }

    [Fact]
    public async Task PostLogin_WithSeededAdminCredentials_RedirectsAndIssuesAuthCookie()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var ct = TestContext.Current.CancellationToken;

        // Credentials match the seed user in ApplicationDbContext + README. If
        // the Identity password-hasher version drifts or seed data is reset,
        // this fails — and the README-documented quick-start is broken.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Email"] = "admin@mcpmanager.local",
                ["Password"] = "123456",
            }
        );

        var response = await client.PostAsync("/Auth/Login", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        // The failure path re-renders the form (200) or redirects back to
        // /auth/login; success goes to Home (collapses to "/" because Home is
        // the default route).
        response.Headers.Location!.ToString().Should().NotContainEquivalentOf("auth/login");

        // Without this the test would also pass for any benign 302 that didn't
        // actually authenticate the user.
        var setCookies = response.Headers.GetValues("Set-Cookie");
        setCookies
            .Should()
            .Contain(c => c.StartsWith("Identity.Application=", StringComparison.Ordinal));
    }
}
