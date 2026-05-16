using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class AuthControllerLoginModelStateInvalidTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public AuthControllerLoginModelStateInvalidTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostLogin_BlankEmail_ReRendersFormWithoutSigningIn()
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
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("form input[name='AntiForgery']")!.GetAttribute("value")!;

        // Existing Login tests post a [Required]-valid email so they skip the
        // `!ModelState.IsValid -> View(loginDto)` guard. A blank email fails
        // [Required]/[EmailAddress], so the form must re-render (HTTP 200) and
        // never reach FindByNameAsync/sign-in — a regression skipping the gate
        // would NRE on a null username lookup.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Email"] = "",
                ["Password"] = "whatever",
            }
        );

        var response = await client.PostAsync("/Auth/Login", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
