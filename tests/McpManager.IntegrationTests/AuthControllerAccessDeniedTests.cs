using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class AuthControllerAccessDeniedTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public AuthControllerAccessDeniedTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetAccessDenied_Anonymous_RendersForbiddenPage()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var ct = TestContext.Current.CancellationToken;

        // AccessDenied is the cookie-auth access-denied redirect target and was
        // entirely zero-hit. It must render anonymously (the controller is
        // [AllowAnonymous]) — a regression breaking the view or its asp-action
        // "Back" link would 500 the page every authorization failure lands on.
        var response = await client.GetAsync("/Auth/AccessDenied", ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("You don't have permission to access this page.");
        body.Should().Contain("403");
    }
}
