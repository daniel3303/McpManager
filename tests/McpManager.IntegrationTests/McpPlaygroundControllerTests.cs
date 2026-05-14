using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpPlaygroundControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpPlaygroundControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetGetTools_WithUnknownServerId_Returns404()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            }
        );
        var ct = TestContext.Current.CancellationToken;

        await _factory.SignInAsAdminAsync(client, ct);

        // GetTools returns NotFound() when the server lookup misses — the only
        // 404 path in this controller. A regression that returns 500 (unhandled
        // null) or 200 with an empty list would hide the missing-server case
        // from the playground UI's error toast.
        var response = await client.GetAsync($"/McpPlayground/GetTools/{Guid.NewGuid()}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
