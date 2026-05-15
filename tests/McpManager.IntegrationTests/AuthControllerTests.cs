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
}
