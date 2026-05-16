using System.Net.Http.Headers;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class ApiKeyAuthHandlerNonBearerHeaderTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ApiKeyAuthHandlerNonBearerHeaderTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task McpEndpoint_WithNonBearerAuthorizationHeader_IsRejected()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var ct = TestContext.Current.CancellationToken;

        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}",
                System.Text.Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");

        // A present-but-non-"Bearer " Authorization header makes ExtractApiKey
        // fall through to `return null` (zero-hit: every auth test uses Bearer
        // or no header). The handler must still Fail — a regression accepting
        // any scheme would let Basic/other credentials reach upstream tools.
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", "dXNlcjpwYXNz");

        var response = await client.SendAsync(request, ct);

        response
            .IsSuccessStatusCode.Should()
            .BeFalse("a non-Bearer Authorization header must not authenticate");
        ((int)response.StatusCode)
            .Should()
            .BeInRange(401, 403, "the rejection must be an auth failure (401/403)");
    }
}
