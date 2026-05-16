using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpRequestsControllerShowNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpRequestsControllerShowNotFoundTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetShow_UnknownId_RedirectsToIndex()
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

        // Show uses a path-segment route ([HttpGet("{id:guid}")]) so the
        // action runs; its `request == null -> RedirectToAction(Index)` guard
        // was zero-hit (the existing Show test always seeds the request). A
        // regression dropping it would NRE rendering the details view.
        var response = await client.GetAsync($"/McpRequests/Show/{Guid.NewGuid()}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/mcprequests");
    }
}
