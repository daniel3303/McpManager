using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class AuthControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public AuthControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task PostLogin_WithWrongPassword_ReRendersWithInvalidLoginMessage()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            }
        );
        var ct = TestContext.Current.CancellationToken;

        var getResp = await client.GetAsync("/Auth/Login", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = document
            .QuerySelector("form input[name='AntiForgery']")!
            .GetAttribute("value")!;

        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Email"] = "admin@mcpmanager.local",
                ["Password"] = "definitely-the-wrong-password",
            }
        );

        // The failed-credentials fall-through (lines 56-57) was uncovered: a
        // valid+active user whose PasswordSignInAsync fails must re-render the
        // form (200, not 302) with the InvalidLogin message — never redirect to
        // Home. A regression here would either 500 or silently log a bad actor in.
        var response = await client.PostAsync("/Auth/Login", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("Invalid credentials or inactive account.");
    }

    [Fact]
    public async Task GetLogout_WhenAuthenticated_SignsOutAndRedirectsHome()
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

        // Logout (lines 62-66) was uncovered: SignOutAsync + SignOut with a
        // RedirectUri to Home. After it, the auth cookie must be cleared so a
        // follow-up request to a protected page bounces to the login page — a
        // regression leaving the cookie alive would keep a "logged out" user in.
        var logoutResp = await client.GetAsync("/Auth/Logout", ct);
        logoutResp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var afterLogout = await client.GetAsync("/Home", ct);
        afterLogout.StatusCode.Should().Be(HttpStatusCode.Redirect);
        afterLogout
            .Headers.Location!.ToString()
            .Should()
            .ContainEquivalentOf("/auth/login", "a signed-out session must be bounced to login");
    }
}
