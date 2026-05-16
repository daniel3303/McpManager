using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class HomeControllerErrorTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public HomeControllerErrorTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetError_NoExceptionFeature_RendersErrorViewAnonymously()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var ct = TestContext.Current.CancellationToken;

        // Error() is [AllowAnonymous] and reads IExceptionHandlerFeature, which
        // is null on a direct GET (no exception was handled). That whole action
        // was zero-hit. A regression that dereferenced the null feature, or
        // dropped [AllowAnonymous] (redirecting the error page to login), would
        // break the user-facing error screen.
        var response = await client.GetAsync("/Home/Error", ct);

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(ct);
        // Title is set by the action (ViewData["Title"] = "Error") and the
        // layout renders it; this holds whether or not an exception exists.
        html.Should().Contain("Error - MCP Manager", "the Error view must render");
    }
}
