using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class ApiKeysControllerEditGetNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ApiKeysControllerEditGetNotFoundTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetEdit_UnknownId_ReturnsNotFound()
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

        // Edit GET uses a path-segment route ([HttpGet("{id:guid}")]) so the
        // action runs; its `apiKey == null -> NotFound()` guard was zero-hit
        // (existing Edit tests always seed the key). A regression dropping it
        // would NRE building the _EditForm DTO for a stale id.
        var response = await client.GetAsync($"/ApiKeys/Edit/{Guid.NewGuid()}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
