using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class LiveLogsControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public LiveLogsControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetPoll_AsAuthenticatedAdmin_ReturnsCamelCaseEntriesJson()
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

        var response = await client.GetAsync("/LiveLogs/Poll", ct);

        // The action returns Json(new { Entries = ... }) — the PascalCase
        // property must serialise as camelCase "entries" via the
        // CamelCasePropertyNamesContractResolver in Program.cs. A regression
        // here would break the live-logs polling script in the portal UI.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("\"entries\"");
        body.Should().NotContain("\"Entries\"");
    }
}
