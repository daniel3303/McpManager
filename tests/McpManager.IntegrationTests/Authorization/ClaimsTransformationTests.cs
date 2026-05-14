using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests.Authorization;

public class ClaimsTransformationTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ClaimsTransformationTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task AuthenticatedAdmin_AccessingPolicyProtectedEndpoint_GetsOk()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            }
        );
        var ct = TestContext.Current.CancellationToken;

        // Sign in as the seeded admin. The seed grants only the "Admin" claim;
        // policies like "McpServers" succeed only because ClaimsTransformation
        // expands Admin into every other ClaimStore claim on each request.
        var loginForm = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Email"] = "admin@mcpmanager.local",
                ["Password"] = "123456",
            }
        );
        var loginResponse = await client.PostAsync("/Auth/Login", loginForm, ct);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Found);

        var response = await client.GetAsync("/McpServers", ct);

        // A regression that removes the Admin-grants-all branch would
        // surface here as a 302 to /auth/accessdenied.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
