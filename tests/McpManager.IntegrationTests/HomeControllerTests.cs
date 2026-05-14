using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Xunit;

namespace McpManager.IntegrationTests;

public class HomeControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public HomeControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task Index_AnonymousRequest_RedirectsToLoginWithReturnUrl()
    {
        var client = _factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            }
        );
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/Home/Index", ct);

        // OnRedirectToLogin in Program.cs uses LinkGenerator + Debug.Assert.
        // In Release builds the assert is stripped, so a null path would
        // redirect to "" — this test pins the resolved URL.
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        var location = response.Headers.Location!.ToString();
        // Routing has LowercaseUrls = true, so paths come back lowercased.
        location.Should().StartWithEquivalentOf("/auth/login");
        location.Should().Contain("ReturnUrl=");
        location.Should().ContainEquivalentOf("home");
    }
}
