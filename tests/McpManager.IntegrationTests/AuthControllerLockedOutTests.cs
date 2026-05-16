using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class AuthControllerLockedOutTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public AuthControllerLockedOutTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetLockedOut_Anonymous_RendersLockedPage()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var ct = TestContext.Current.CancellationToken;

        // LockedOut is the lockout redirect target and was entirely zero-hit.
        // It must render anonymously ([AllowAnonymous]) with the controller's
        // ViewData["Text"] surfaced — a regression breaking the action's
        // ViewData wiring or the view would 500 the page a locked-out sign-in
        // lands on.
        var response = await client.GetAsync("/Auth/LockedOut", ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("Your account is locked.");
    }
}
