using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class ApiKeyAuthHandlerInvalidKeyTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ApiKeyAuthHandlerInvalidKeyTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task McpEndpoint_WithUnknownBearerKey_IsRejected()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var ct = TestContext.Current.CancellationToken;

        // POST so the streamable-HTTP /mcp endpoint matches the route (GET
        // 404s before auth runs); RequireAuthorization then invokes the
        // ApiKey handler before the MCP body is parsed.
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
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            $"nope-{Guid.NewGuid():N}"
        );

        // ExtractApiKey returns the token, GetByKey finds no match, so the
        // `apiKey == null -> Fail("Invalid API key")` branch runs. It was
        // zero-hit (auth tests use a seeded key). A regression that treated an
        // unknown key as authenticated would expose every upstream MCP tool to
        // anyone with any Bearer string.
        var response = await client.SendAsync(request, ct);

        response
            .IsSuccessStatusCode.Should()
            .BeFalse("an unknown API key must be rejected, not authenticated");
        ((int)response.StatusCode)
            .Should()
            .BeInRange(401, 403, "the rejection must be an auth failure (401/403)");
    }
}
