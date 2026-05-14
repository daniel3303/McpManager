using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class ApiKeyAuthHandlerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ApiKeyAuthHandlerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task PostMcp_WithoutAuthorizationHeader_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        // The /mcp endpoint is the only externally reachable surface that
        // serves tools to AI clients — any regression that lets it accept
        // anonymous traffic exposes every registered upstream server.
        var response = await client.PostAsync("/mcp", content: null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
