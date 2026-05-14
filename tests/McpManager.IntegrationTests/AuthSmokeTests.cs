using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
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
        var document = await BrowsingContext.New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);

        // Asserting on the resolved action attribute catches typos in
        // asp-action / asp-controller — a broken tag helper renders action=""
        // instead of throwing.
        var form = document.QuerySelector("form#loginForm");
        form.Should().NotBeNull("the login view must render the login form");
        form.GetAttribute("action").Should().BeEquivalentTo("/Auth/Login");
    }
}
