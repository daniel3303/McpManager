using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpRequestsControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpRequestsControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetIndex_WithPageBeyondAvailableData_Returns200()
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

        // The fixture DB has zero seeded McpToolRequest rows. Index applies
        // Skip((page - 1) * pageSize) and Ceiling(totalCount / pageSize);
        // a regression that crashes on totalCount = 0 or treats page > totalPages
        // as an error would surface as a 5xx here. The action must render the
        // empty-state grid for both page=1 and page=N>totalPages.
        var response = await client.GetAsync("/McpRequests?page=5", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
